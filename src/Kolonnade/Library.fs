namespace Kolonnade

open System
open System.Drawing
open System.Runtime.InteropServices


// Opaque type to hide the underlying IntPtr
[<NoComparison; StructuralEquality>]
type Id = private Id of IntPtr

// Used for information purposes (i.e. quick window jump), but not for internal bookkeeping
type Window<'I when 'I: null> internal (process_name: string option,
                                        desktop: VirtualDesktop.Desktop,
                                        hWnd: User32.HWND,
                                        title: String,
                                        icon: 'I option) =
   member this.Desktop = desktop
   member this.Process = Option.toObj process_name
   member this.Title = title
   member this.Icon = Option.toObj icon
   member this.Id = Id hWnd
   member this.ToForeground() =
       // XXX maximize if minimized?
       User32.SetForegroundWindow(hWnd)
       |> ignore

type internal WorldEvent =
    | DesktopChanged
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
    let mutable stackSet =
        StackSet.fromDesktops<User32.HWND, Layout> (desktopManager, List.ofSeq (DisplayUtils.getDisplays()), Full())
    let mutable moving: User32.HWND option = None

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
            let flags = uint32 (User32.SwpFlags.NoActivate ||| User32.SwpFlags.NoZOrder)
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
    let moveWindowsBackToBelongingDesktop (newDesktop: VirtualDesktop.Desktop) =
        for display in stackSet.Displays() do
            if display.workspace.tag <> newDesktop.N then
                let targetDesktop = desktopManager.GetDesktops().[display.workspace.tag - 1]
                display.workspace.stack
                |> Option.iter (fun stack -> List.iter (fun w -> moveToDesktopIfRequired targetDesktop w) (stack.ToList()))

    let refresh() =
        let currentVirtualDesktop = desktopManager.GetCurrentDesktop()
        // For each display, layout the currently visible workspace
        for display in stackSet.Displays() do
            display.workspace.stack
            |> Option.iter (fun stack ->
                // XXX how to handle here that the display area can't be determined?
                let displayArea = DisplayUtils.rectangleFromMonitorHandle display.monitor
                for (w, area) in display.workspace.layout.DoLayout(stack, displayArea.Value) do
                    printfn "%A %A %A" w area (WinUtils.windowTitle w)
                    // This is where integrating seamless into Windows fails a bit: we want to display
                    // multiple workspaces at once, one at each display, but a virtual desktop spans
                    // over all displays. Solution: temporarily move windows around a bit.
                    moveToDesktopIfRequired currentVirtualDesktop w
                    setWindowPosIfRequired w area)
        stackSet.Peek() |> Option.iter (fun hWnd -> User32.SetForegroundWindow(hWnd) |> ignore)

    let manage hWnd =
        stackSet <- stackSet.InsertUp(hWnd)
        refresh()

    // Initialization
    do
        for hWnd in WinUtils.windows interesting do
            match desktopManager.GetDesktop(hWnd) with
            | Some desktop ->
                stackSet <- stackSet.View(desktop.N).InsertUp(hWnd)
            | None -> ()
        stackSet <- stackSet.View(1)
        refresh()

    let switch_to_desktop = function
    | i when i < 0 -> ()
    | i ->
        let desktops = desktopManager.GetDesktops()
        if i < desktops.Count then
            desktops.[i].SwitchTo()

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

    let handleMainWindowSizeChange hWnd =
        match DisplayUtils.rectFromMonitorHandle stackSet.current.monitor with
        | Some rect ->
            // XXX this should be handled by current layout itself
            let windowRect = WinUtils.windowFrameRect hWnd
            let windowWidth = windowRect.right - windowRect.left
            let displayWidth = rect.right - rect.left
            let newFraction = float windowWidth / float displayWidth
            if stackSet.current.workspace.layout.GetType() = typeof<Tall> then
                stackSet <-
                    { stackSet with current = { stackSet.current with workspace = { stackSet.current.workspace with layout = Tall(newFraction) } } }
                refresh()
        | None -> ()

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
            | User32.WinEvent.ObjectLocationchange when isWindow -> manager.HandleEvent(WindowChangedLocation hWnd)
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
        WinUtils.windows User32.IsWindowVisible
        |> seq<User32.HWND>
        |> Seq.map (fun hWnd -> (desktopManager.GetDesktop(hWnd), hWnd, WinUtils.windowTitle hWnd))
        |> Seq.collect (function
            | (None, _, _) -> Seq.empty
            | (_, _, None) -> Seq.empty
            | (Some(desktop), hWnd, Some(title)) ->
                let process_name = determineProcess (hWnd)
                let icon = getIcon hWnd
                Seq.singleton (Window(cleanUpProcessName process_name, desktop, hWnd, title, icon))
            )

    member this.SwitchToDesktop(i) =
        switch_to_desktop i

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

    /// Raises the currently focused window to the main pane.
    /// The other windows are kept in order and shifted down on the stack.
    member this.RaiseToMain() =
        stackSet <- stackSet.RaiseToMain()
        refresh()

    member this.FocusUp() =
        stackSet <- stackSet.FocusUp()
        refresh()

    member this.FocusDown() =
        stackSet <- stackSet.FocusDown()
        refresh()

    member this.FocusMain() =
        stackSet <- stackSet.FocusMain()
        refresh()

    member this.CycleLayout() =
        let newLayout: Layout =
            if stackSet.current.workspace.layout.GetType() = typeof<Tall> then Full() :> Layout
            else Tall(0.7) :> Layout
        stackSet <-
            { stackSet with current = { stackSet.current with workspace = { stackSet.current.workspace with layout = newLayout } } }
        refresh()

    member internal this.HandleEvent(event: WorldEvent) =
        match event with
        | DesktopChanged ->
            let currentDesktop = desktopManager.GetCurrentDesktop()
            moveWindowsBackToBelongingDesktop currentDesktop
            stackSet <- stackSet.View(currentDesktop.N)
            refresh()
        | WindowCreated hWnd when interesting hWnd -> manage hWnd
        | WindowDestroyed hWnd ->
            stackSet <- stackSet.Delete(hWnd)
            if onCurrentDesktop hWnd then
                refresh()
        | WindowChangedLocation hWnd when stackSet.Contains(hWnd) && moving <> Some(hWnd) ->
            if onCurrentDesktop hWnd then
                if User32.IsIconic(hWnd) then
                    User32.ShowWindow(hWnd, User32.SW_RESTORE) |> ignore
                else
                    refresh()
        // XXX desktop change
        | WindowFocused hWnd -> stackSet <- stackSet.Focus(hWnd)
        | WindowShown hWnd when interesting hWnd -> manage hWnd
        | WindowMoveSizeStart hWnd -> moving <- Some hWnd
        | WindowMoveSizeEnd hWnd ->
            match stackSet.current.workspace.stack with
            | Some { focus = f; up = [] } when f = hWnd ->
                // Main window was changed, let layout handle that
                handleMainWindowSizeChange hWnd
            | _ -> refresh()
            moving <- None
        | WindowCloaked hWnd ->
            match (desktopManager.GetDesktop(hWnd), stackSet.FindWorkspace(hWnd)) with
            | (Some d, Some ws) when d.N <> ws.tag && not (stackSet.IsOnSomeDisplay(ws.tag)) ->
                // Window was moved to a different virtual desktop and it was not caused by Kolonnade itself
                stackSet <- stackSet.Delete(hWnd).OnWorkspace(d.N, fun s -> s.InsertUp(hWnd))
                refresh()
            | _ -> ()
        | _ -> ()
