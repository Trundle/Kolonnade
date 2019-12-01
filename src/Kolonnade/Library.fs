namespace Kolonnade

open System
open System.Drawing
open System.Runtime.InteropServices


// Opaque type to hide the underlying IntPtr
[<NoComparison; StructuralEquality>]
type Id = private Id of IntPtr

// Used for information purposes (i.e. quick window jump), but not for internal bookkeeping
type Window<'I when 'I: null> internal (process_name: string option,
                                        desktop: int,
                                        hWnd: User32.HWND,
                                        title: String,
                                        icon: 'I option,
                                        focus: User32.HWND -> unit) =
   member this.Desktop = desktop
   member this.Process = Option.toObj process_name
   member this.Title = title
   member this.Icon = Option.toObj icon
   member this.Id = Id hWnd
   member this.ToForeground() = focus(hWnd)

type internal WorldEvent =
    | DesktopChanged
    | DisplaysChanged
    | TitleChanged of User32.HWND
    | WindowCreated of User32.HWND
    | WindowDestroyed of User32.HWND
    | WindowChangedLocation of User32.HWND
    | WindowCloaked of User32.HWND
    | WindowFocused of User32.HWND
    | WindowShown of User32.HWND
    | WindowMoveSizeStart of User32.HWND
    | WindowMoveSizeEnd of User32.HWND

// Parametrized on icon type, as this library doesn't have a dependency to WPF
type WindowManager<'I when 'I: null> internal (desktopManager: VirtualDesktop.Manager,
                                               iconLoader: User32.HWND -> 'I) =
    let processNameCache = LRUCache<User32.HWND, string option>(512)
    let window_icon_cache = LRUCache<User32.HWND, 'I option>(512)
    let windowTitleCache = LRUCache<User32.HWND, string>(512)
    let windowActivityTracker = ActivityTracker<User32.HWND>((3, TimeSpan.FromSeconds(1.0)), fun () -> DateTime.Now)
    let mutable stackSet =
        let layout = Choose.between(Full(), TwoPane(0.5), Tall(0.7), Rotated(TwoPane(0.5)), Rotated(Tall(0.7)))
        StackSet.fromDesktops<User32.HWND, Layout> (desktopManager, List.ofSeq (DisplayUtils.getDisplays()), layout)
    let mutable moving: (User32.HWND * User32.RECT) option = None
    let movingHwnd() = Option.map (fun (hWnd, _) -> hWnd) moving

    /// Returns whether the given hWnd should be handled by Kolonnade.
    let interesting hWnd =
        let style: User32.WindowStyle = enum (int (User32.GetWindowLongPtr(hWnd, User32.GWL_STYLE).ToInt64()))
        let exStyle: User32.ExtendedWindowStyle = enum (int (User32.GetWindowLongPtr(hWnd, User32.GWL_EXSTYLE).ToInt64()))

        style.HasFlag(User32.WindowStyle.MaximizeBox)
        && not (style.HasFlag(User32.WindowStyle.Child))
        && not (exStyle.HasFlag(User32.ExtendedWindowStyle.ToolWindow))
        && User32.IsWindowVisible(hWnd)

    let setWindowPosIfRequired hWnd (area: Rectangle) =
        let actualRect = WinUtils.windowFrameRect hWnd

        if area.Top <> actualRect.top || area.Bottom <> actualRect.bottom
           || area.Left <> actualRect.left || area.Right <> actualRect.right
           then
            // Set position, but also count in DWM frame size
            let mutable windowRect: User32.RECT = { left = 0; right = 0; top = 0; bottom = 0 }
            User32.GetWindowRect(hWnd, &windowRect) |> ignore

            let leftBorderWidth = actualRect.left - windowRect.left
            let rightBorderWidth = windowRect.right - actualRect.right
            let topBorderWidth = actualRect.top - windowRect.top
            let bottomBorderWidth = windowRect.bottom - actualRect.bottom
            let flags = uint32 (User32.SwpFlags.NoActivate ||| User32.SwpFlags.ShowWindow)
            User32.SetWindowPos (hWnd, IntPtr.Zero,
                                 area.Left - leftBorderWidth,
                                 area.Top - topBorderWidth,
                                 area.Width + leftBorderWidth + rightBorderWidth,
                                 area.Height + topBorderWidth + bottomBorderWidth,
                                 flags)
            |> ignore

    let moveToDesktopIfRequired (targetDesktop: VirtualDesktop.Desktop) win =
        match desktopManager.GetDesktop(win) with
        | Some(currentDesktop) when currentDesktop.N <> targetDesktop.N -> targetDesktop.MoveWindowTo(win)
        | _ -> ()

    /// Moves all windows back to the desktop where they originally belonged to, but were moved
    /// due to Kolonnade's multi-workspace support.
    /// This should be called before the stackSet has been updated after a desktop switch.
    let moveWindowsBackToBelongingDesktop (newDesktop: int) =
        let newWorkspaces = Set <| List.map (fun d -> d.workspace.tag) (stackSet.View(newDesktop).Displays())
        for display in stackSet.Displays() do
            let tag = display.workspace.tag
            if not(newWorkspaces.Contains(tag)) then
                let targetDesktop = desktopManager.GetDesktops().[tag - 1]
                display.workspace.stack
                |> Option.iter (fun stack -> List.iter (fun w -> moveToDesktopIfRequired targetDesktop w) (stack.ToList()))

    let moveToBackground hWnd =
        let flags = uint32 (User32.SwpFlags.NoActivate ||| User32.SwpFlags.NoMove ||| User32.SwpFlags.NoSize)
        User32.SetWindowPos(hWnd, User32.HWND_BOTTOM, 0, 0, 0, 0, flags) |> ignore

    let refresh() =
        // This is where integrating seamless into Windows fails a bit: we want to display multiple
        // workspaces at once, one at each display, but a virtual desktop spans over all displays.
        // Solution: temporarily move windows around a bit. The target desktop is either the
        // current virtual desktop (if it corresponds to a workspace), otherwise to the virtual
        // desktop of the current workspace.
        let targetDesktop =
            match desktopManager.GetCurrentDesktop() with
            | d when stackSet.IsOnSomeDisplay(d.N) -> d
            | _ -> desktopManager.GetDesktops().[stackSet.current.workspace.tag - 1]
        // For each display, layout the currently visible workspace
        for display in stackSet.Displays() do
            match (display.workspace.stack, DisplayUtils.rectangleFromMonitorHandle display.monitor) with
            | (Some stack, Some displayArea) ->
                let mutable notSeen = Set(stack.ToList())
                for (w, area) in display.workspace.layout.DoLayout(stack, displayArea) do
                    moveToDesktopIfRequired targetDesktop w
                    setWindowPosIfRequired w area
                    notSeen <- notSeen.Remove(w)
                for w in notSeen do
                    moveToBackground w
            | _ -> ()
        // Activate focused window or switch to desktop if stack is empty
        match stackSet.Peek() with
        | Some hWnd -> User32.SetForegroundWindow(hWnd) |> ignore
        | None ->
            let currentTag = stackSet.current.workspace.tag
            if stackSet.visible.IsEmpty && desktopManager.GetCurrentDesktop().N <> currentTag
            then desktopManager.GetDesktops().[currentTag - 1].SwitchTo()

    let manage hWnd =
        stackSet <- stackSet.InsertUp(hWnd)
        WinUtils.windowTitle hWnd |> Option.iter (fun title -> windowTitleCache.Add(hWnd, title))
        refresh()

    let desktopForTag tag =
        if stackSet.ContainsTag(tag) then Some(desktopManager.GetDesktops().[tag - 1])
        else None

    // Initialization
    do
        for hWnd in WinUtils.windows interesting do
            match desktopManager.GetDesktop(hWnd) with
            | Some desktop ->
                stackSet <- stackSet.View(desktop.N).InsertUp(hWnd)
                WinUtils.windowTitle hWnd |> Option.iter (fun title -> windowTitleCache.Add(hWnd, title))
            | None -> ()
        stackSet <- stackSet.View(1)
        refresh()

    let determineProcess hWnd =
        match processNameCache.Get(hWnd) with
        | Some(value) -> value
        | None ->
            let name = WinUtils.windowProcess hWnd
            processNameCache.Add(hWnd, name)
            name

    let getIcon hWnd =
        match window_icon_cache.Get(hWnd) with
        | Some(value) -> value
        | None ->
            let icon = WinUtils.windowIcon hWnd iconLoader
            window_icon_cache.Add(hWnd, icon)
            icon

    let cleanUpProcessName = function
    | Some(name: string) ->
        let parts = name.Split [| '\\' |]
        // Drop first two elements, as they are uninteresting
        let concat = String.concat "\\" (Array.sub parts 3 (parts.Length - 3))
        if concat.Length > 99 then
            Some("…" + concat.Substring(concat.Length - 99))
        else Some(concat)
    | None -> None

    /// Returns whether the given window is on the current (virtual) desktop.
    let onCurrentDesktop hWnd =
        match (desktopManager.GetDesktop(hWnd), desktopManager.GetCurrentDesktop()) with
        | (Some windowDesktop, currentDesktop) when windowDesktop.N = currentDesktop.N -> true
        | _ -> false

    /// Returns the workspace for the given display number.
    let findWorkspaceForDisplay n =
        stackSet.Displays()
        |> List.tryFind (fun d -> d.n = n)
        |> Option.map (fun d -> d.workspace)

    let handleDisplayChange() =
        stackSet <- StackSet.fromCurrentAndDisplays(stackSet, List.ofSeq(DisplayUtils.getDisplays()))
        refresh()

    static let registerWinEventHook (manager: WindowManager<'I>) =
        let hook = User32.WINEVENTPROC(fun _ event hWnd idObject _ _ _ ->
            let isWindow = idObject = int User32.ObjId.Window
            match enum event with
            | User32.WinEvent.ObjectNamechange when isWindow ->
                if hWnd = User32.GetDesktopWindow() then
                    // This means the virtual desktop was switched (because that updates the desktop window's
                    // accessibility name)
                    manager.HandleEvent(DesktopChanged)
                else manager.HandleEvent(TitleChanged(hWnd))
            | User32.WinEvent.SystemMoveSizeStart -> manager.HandleEvent(WindowMoveSizeStart hWnd)
            | User32.WinEvent.SystemMoveSizeEnd -> manager.HandleEvent(WindowMoveSizeEnd hWnd)
            | User32.WinEvent.ObjectCreate when isWindow -> manager.HandleEvent(WindowCreated hWnd)
            | User32.WinEvent.ObjectDestroy when isWindow -> manager.HandleEvent(WindowDestroyed hWnd)
            | User32.WinEvent.ObjectLocationchange when isWindow ->
                if hWnd = User32.GetShellWindow() then
                    // If the shell window changes size (location), either the DPI for a display
                    // was changed or a display was added or removed.
                    manager.HandleEvent(DisplaysChanged)
                else manager.HandleEvent(WindowChangedLocation hWnd)
            | User32.WinEvent.ObjectCloaked when isWindow -> manager.HandleEvent(WindowCloaked hWnd)
            | User32.WinEvent.ObjectShow when isWindow -> manager.HandleEvent(WindowShown hWnd)
            // XXX needs to be handled?
            | User32.WinEvent.ObjectHide when isWindow -> printfn "[DEBUG] Window hidden: %A" (WinUtils.windowTitle hWnd)
            | User32.WinEvent.SystemForeground -> manager.HandleEvent(WindowFocused hWnd)
            | _ -> ())
        // Keep hook forever alive
        GCHandle.Alloc(hook) |> ignore
        let flags = User32.WinEventFlags.WINEVENT_OUTOFCONTEXT ||| User32.WinEventFlags.WINEVENT_SKIPOWNPROCESS
        if User32.SetWinEventHook(int User32.WinEvent.Min, int User32.WinEvent.Max, IntPtr.Zero, hook, 0, 0,
                                  int flags) = IntPtr.Zero then
            printfn "[ERROR] Could not register win event hook :( :("

    static member New(iconLoader: System.Func<User32.HWND, 'I>) =
        let manager = WindowManager(VirtualDesktop.newManager(), fun h -> iconLoader.Invoke(h))
        registerWinEventHook manager
        manager

    static member EmptyRect = Rectangle(0, 0, 0, 0)

    member this.GetWindows() =
        let focus hWnd = this.ModifyStackSet(fun s -> s.Focus(hWnd))
        stackSet.Workspaces()
        |> List.map
               (fun w ->
                        let entry hWnd = (cleanUpProcessName (determineProcess hWnd),
                                          w.tag,
                                          hWnd,
                                          windowTitleCache.Get(hWnd),
                                          getIcon hWnd)
                        match w.stack with
                        | Some stack -> List.map entry (stack.ToList())
                        | None -> [])
        |> List.fold (@) []
        |> Seq.collect (function
            | (_, _, _, None, _) -> Seq.empty
            | (processName, tag, hWnd, Some title, icon) ->
                Window(processName, tag, hWnd, title, icon, focus)
                |> Seq.singleton)

    member this.SwitchToWorkspace(i) =
        moveWindowsBackToBelongingDesktop i
        stackSet <- stackSet.View(i)
        refresh()

    member this.GetActiveMonitor() =
        match User32.GetForegroundWindow() with
        | hWnd when hWnd <> IntPtr.Zero ->
            match User32.MonitorFromWindow(hWnd, User32.MONITOR_DEFAULTTONULL) with
            | monitorHandle when monitorHandle <> IntPtr.Zero ->
                match DisplayUtils.rectFromMonitorHandle monitorHandle
                      |> Option.map (DisplayUtils.toWpfPixels monitorHandle) with
                | Some(rect) -> Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom)
                | None -> WindowManager<'I>.EmptyRect
            | _ -> WindowManager<'I>.EmptyRect
        | _ -> WindowManager<'I>.EmptyRect

    member this.ModifyStackSet(f: System.Func<StackSet<User32.HWND, Layout>, StackSet<User32.HWND, Layout>>) =
        stackSet <- f.Invoke(stackSet)
        refresh()

    /// Moves the focused element of the current stack to the workspace with the given
    /// tag. Returns the same StackSet if the stack is empty. Doesn't change the focused
    /// workspace.
    member this.Shift(tag) =
        stackSet.Peek()
        |> Option.iter (fun w ->
            stackSet <- stackSet.Shift(tag)
            desktopForTag tag |> Option.iter (fun desktop -> desktop.MoveWindowTo(w))
            refresh())

    member this.ViewDisplay(n) =
        findWorkspaceForDisplay n
        |> Option.iter (fun ws ->
            stackSet <- stackSet.View(ws.tag)
            refresh())

    /// Activate Kolonnade by injecting a hotkey keypress.
    /// Windows grants apps that received a hotkey message certain rights, such as setting the
    /// foreground window, which is something Kolonnade needs. Hence instead of doing actions
    /// right away, hotkey keypresses are injected, Windows then sends an activation message
    /// to Kolonnade and Kolonnade has the rights to shuffle windows around.
    member this.ActivateViaHotkey() =
        let key vk flags : User32.INPUT =
            let ki : User32.KEYBDINPUT =
                { wVk = int16 vk; wScan = int16 vk; dwFlags = flags; time = 0; dwExtraInfo = 0n }
            { ``type`` = User32.InputType.Keyboard; ki = ki; padding = 0; padding2 = 0 }
        let keyDown vk = key vk (enum<User32.KeyEventFlags>(0))
        let keyUp vk = key vk User32.KeyEventFlags.KeyUp
        let input = [|
            keyDown User32.VirtualKey.F13;
            keyUp User32.VirtualKey.F13;
        |]
        User32.SendInput(uint32 input.Length, input, Marshal.SizeOf<User32.INPUT>()) |> ignore

    member this.PostMessage(msg) =
        stackSet.current.workspace.layout.HandleMessage(msg) |> Option.iter (fun newLayout ->
            stackSet <- stackSet.WithCurrentLayout(newLayout)
            refresh())

    /// XXX Assumes that the layout is a Choose
    member this.EnumerateLayouts() =
        let result = System.Collections.Generic.List<Layout>()
        let rec addNext = function
            | Some (layout: Layout) ->
                result.Add(layout)
                addNext(layout.HandleMessage(NextLayoutNoWrap))
            | None -> ()
        addNext(stackSet.current.workspace.layout.HandleMessage(FirstLayout))
        result

    member internal this.HandleEvent(event: WorldEvent) =
        match event with
        | DesktopChanged ->
            let currentDesktop = desktopManager.GetCurrentDesktop()
            moveWindowsBackToBelongingDesktop currentDesktop.N
            stackSet <- stackSet.View(currentDesktop.N)
            refresh()
        | DisplaysChanged -> handleDisplayChange()
        | WindowCreated hWnd when interesting hWnd -> manage hWnd
        | WindowDestroyed hWnd ->
            windowTitleCache.Remove(hWnd)
            let workspace = stackSet.FindWorkspace(hWnd)
            stackSet <- stackSet.Delete(hWnd)
            Option.iter (fun ws -> if stackSet.IsOnSomeDisplay(ws.tag) then refresh()) workspace
        | WindowChangedLocation hWnd when stackSet.Contains(hWnd) && movingHwnd() <> Some hWnd ->
            if onCurrentDesktop hWnd then
                if User32.IsIconic(hWnd) then
                    User32.ShowWindow(hWnd, User32.SW_RESTORE) |> ignore
                else if windowActivityTracker.Track(hWnd) = Boring then
                    refresh()
        // XXX desktop change
        | WindowFocused hWnd -> stackSet <- stackSet.Focus(hWnd)
        | WindowShown hWnd when interesting hWnd -> manage hWnd
        | WindowMoveSizeStart hWnd -> moving <- Some (hWnd, WinUtils.windowFrameRect hWnd)
        | WindowMoveSizeEnd hWnd when moving.IsSome ->
            match stackSet.current.workspace.stack with
            | Some stack when stack.Contains(hWnd) ->
                let (hWnd, previousRect) = moving.Value
                let rect = (WinUtils.windowFrameRect hWnd).ToRectangle()
                this.PostMessage(ResizeMessage.WindowSizeChanged(stack, hWnd, previousRect.ToRectangle(), rect))
                refresh()
            | _ -> ()
            moving <- None
        | WindowCloaked hWnd ->
            match (desktopManager.GetDesktop(hWnd), stackSet.FindWorkspace(hWnd)) with
            | (Some d, Some ws) when d.N <> ws.tag && not (stackSet.IsOnSomeDisplay(ws.tag)) ->
                // Window was moved to a different virtual desktop and it was not caused by Kolonnade itself
                stackSet <- stackSet.ShiftWin(d.N, hWnd)
                refresh()
            | _ -> ()
        | TitleChanged hWnd ->
            WinUtils.windowTitle hWnd |> Option.iter (fun title -> windowTitleCache.Add(hWnd, title))
        | _ -> ()
