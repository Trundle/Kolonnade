namespace Kolonnade

open System


// This is mostly directly taken from XMonad and makes heavily use of zippers, first described by Huet.
// See https://web.archive.org/web/20091201114414/https://donsbot.wordpress.com/2007/05/17/roll-your-own-window-manager-tracking-focus-with-a-zipper/
// for details.


module private Util =
    /// Returns the result of applying the given function until the given predicate holds.
    let rec until p f = function
    | x when p x -> x
    | x -> until p f (f x)

open Util


type HMONITOR = IntPtr

/// A stack is a cursor onto a window list. The main window is by convention the top-most item.
type Stack<'A> when 'A : equality =
    { focus: 'A
      up: 'A list
      down: 'A list }

    member this.Contains(w) = List.contains w (this.ToList())

    member this.Filter(p) =
        match List.filter p (this.focus :: this.down) with
        | f :: ds -> Some { focus = f; up = List.filter p this.up; down = ds  }
        | [] ->
            match List.filter p this.up with
            | f :: us -> Some { focus = f; up = us; down = [] }
            | [] -> None

    member this.FocusUp() =
        match this.up with
        | u :: us -> { focus = u; up = us; down = this.focus :: this.down }
        | [] ->
            match List.rev (this.focus :: this.down) with
            | x :: xs -> { focus = x; up = xs; down = [] }
            | _ -> failwith "Not possible to reach, but also not possible to let F# know :("

    member this.SwapUp() =
        match this.up with
        | u :: us -> { focus = this.focus; up = us; down = u :: this.down }
        | [] -> { focus = this.focus; up = List.rev this.down; down = [] }

    /// Reverses this stack: up becomes down and down becomes up
    member this.Reverse() = { focus = this.focus; down = this.up; up = this.down }

    member this.ToList() = List.rev this.up @ this.focus :: this.down


type Workspace<'W, 'L> when 'W : equality =
    { tag: int // Note: starts at 1
      stack: Stack<'W> option
      layout: 'L
      desktop: VirtualDesktop.Desktop }

/// A visible workspace
type Display<'W, 'L> when 'W : equality =
    { n: int // Note: starts at 1
      workspace: Workspace<'W, 'L>
      monitor: HMONITOR }

/// The actual window manager state: a cursor into a non-empty list of workspaces
type StackSet<'W, 'L when 'W: equality> =
    { current: Display<'W, 'L>
      visible: Display<'W, 'L> list
      hidden: Workspace<'W, 'L> list }

    /// Returns the tag of the currently focused workspace.
    member this.CurrentTag() = this.current.workspace.tag

    /// Returns a list of all displays.
    member this.Displays() = this.current :: this.visible

    /// Returns a list of all workspaces.
    member this.Workspaces() =
        this.current.workspace :: List.map (fun d -> d.workspace) this.visible @ this.hidden

    /// Returns the workspace of the given window.
    member this.FindWorkspace(w) =
        let contains = function
        | None -> false
        | Some(stack: Stack<'W>) -> List.contains w (stack.ToList())

        this.Workspaces()
        |> Seq.tryPick (fun w -> if contains w.stack then Some(w) else None)

    /// Returns whether this StackSet contains the given window.
    member this.Contains(w) = this.FindWorkspace(w).IsSome

    /// Returns whether this StackSet contains the given tag.
    member this.ContainsTag(tag) = this.Workspaces() |> List.map (fun ws -> ws.tag) |> List.contains tag

    /// Returns the focused element of the current stack (if there is one).
    member this.Peek() =
        this.current.workspace.stack |> Option.map (fun s -> s.focus)

    /// Apply a function to modify the current stack. Sets the stack to the given default value
    /// if the current stack is empty.
    member this.Modify(emptyValue, f) =
        let newStack = match this.current.workspace.stack with
                       | None -> emptyValue
                       | Some s -> f s
        { this with current = { this.current with workspace = { this.current.workspace with stack = newStack } } }

    /// Applies the given function to modify the current stack in case it isn't empty.
    member this.Modify'(f) = this.Modify(None, fun s -> Some(f s))

    /// Raises the given window to focus.
    member this.Focus(w) =
        match this.Peek() with
        | Some currentFocus when currentFocus = w -> this
        | _ ->
            match this.FindWorkspace(w) with
            | Some ws ->
                let switchedToWs: StackSet<'W, 'L> = this.View(ws.tag)
                switchedToWs.Modify'(fun s -> until (fun s -> s.focus = w) (fun s -> s.FocusUp()) s)
            | None -> this

    member this.FocusUp() = this.Modify'(fun s -> s.FocusUp())

    member this.FocusDown() = this.Modify'(fun s -> s.Reverse().FocusUp().Reverse())

    member this.FocusMain() =
        this.Modify'(function
            | { up = [] } as stack -> stack
            | s ->
                match List.rev s.up with
                | x :: xs -> { focus = x; up = []; down = xs @ s.focus :: s.down }
                | _ -> failwith "Impossible to reach, but F# doesn't know that :(")

    /// Deletes the given window, if it exists.
    member this.Delete(w) =
        let removeFromWorkspace ws =
            { ws with stack = Option.bind (fun (s : Stack<'W>) -> s.Filter(fun x -> x <> w)) ws.stack }
        let removeFromDisplay d = { d with workspace = removeFromWorkspace d.workspace }
        { this with
                  current = removeFromDisplay this.current;
                  visible = List.map removeFromDisplay this.visible
                  hidden = List.map removeFromWorkspace this.hidden }


    /// Insert a new element into the stack, above the currently focused element.
    /// The new element is given focus and the previously focused element is moved down.
    member this.InsertUp(w) =
        if this.Contains(w) then this
        else
            this.Modify (Some ({ focus = w; up = []; down = [] }),
                         fun { focus = f; up = u; down = d; } ->
                            Some ({ focus = w; up = u; down = f :: d }))

    /// Raises the currently focused window to the main pane.
    /// The other windows are kept in order and shifted down on the stack.
    member this.RaiseToMain() =
        this.Modify'(function
            | { up = [] } as stack -> stack
            | { focus = focus; up = up; down = down } ->
                { focus = focus; up = []; down = List.rev up @ down } )

    /// Swaps the currently focused window and the main pane. Focus stays with the item moved.
    member this.SwapMain() =
        this.Modify'(function
            | { up = [] } as stack -> stack // Already main
            | stack ->
                let upRev = List.rev stack.up
                { focus = stack.focus; up = [];  down = upRev.Tail @ (upRev.Head :: stack.down) } )

    /// Swaps the focused window with the previous. Wraps around if the end of stack is reached.
    member this.SwapUp() = this.Modify'(fun s -> s.SwapUp())

    /// Swaps the focused window with the next. Wraps around if the end of stack is reached.
    member this.SwapDown() = this.Modify'(fun s -> s.Reverse().SwapUp().Reverse())

    /// Moves the focused element of the current stack to the workspace with the given
    /// tag. Returns the same StackSet if the stack is empty. Doesn't change the focused
    /// workspace.
    member internal this.Shift(tag) =
        match this.Peek() |> Option.map (fun w -> this.ShiftWin(tag, w)) with
        | Some s -> s
        | _ -> this

    /// Searches for the given window on all workspaces and moves it to the workspace
    /// with the given tag as focused window. Doesn't change the focused workspace.
    member this.ShiftWin(tag, w) =
        match this.FindWorkspace w with
        | Some ws when ws.tag <> tag && this.ContainsTag(tag) ->
            this.Delete(w).OnWorkspace(tag, fun s -> s.InsertUp(w))
        | _ -> this

    /// Sets focus to the workspace with the given tag. Returns the stackset unmodified
    /// if the given tag doesn't exist.
    member internal this.View(t) =
        // Already focused
        if t = this.CurrentTag() then this
        else
            match List.tryFind (fun d -> d.workspace.tag = t) this.visible with
            | Some x ->
                // Workspace is visible, just raise it
                let newVisible = this.current :: List.filter (fun d -> d.monitor <> x.monitor) this.visible
                { this with current = x; visible = newVisible }
            | None ->
                // Workspace is currently hidden, raise it on the active display
                match List.tryFind (fun w -> w.tag = t) this.hidden with
                | Some x ->
                    let new_hidden = this.current.workspace :: List.filter (fun w -> w.tag <> x.tag) this.hidden
                    { this with current = { this.current with workspace = x }; hidden = new_hidden }
                | None ->
                    // Not even part of this stackset
                    this

    /// Perform the given action on the workspace with the given tag.
    member this.OnWorkspace(t, f: StackSet<'W, 'L> -> StackSet<'W, 'L>): StackSet<'W, 'L> =
        this.View(t) |> f |> fun s -> s.View(this.CurrentTag())

    /// Returns whether the workspace with the given tag is currently visible on some display.
    member this.IsOnSomeDisplay(tag) =
        List.tryFind (fun d -> d.workspace.tag = tag) (this.Displays())
        |> Option.isSome

module internal StackSet =
    /// Constructs a new StackSet from Window's current state
    let fromDesktops<'W, 'L when 'W: equality> (desktopManager: VirtualDesktop.Manager,
                                                displayRects: (HMONITOR * User32.RECT) list,
                                                defaultLayout: 'L): StackSet<'W, 'L> =
        // Simplify initialization 🤷
        desktopManager.GetDesktops().[0].SwitchTo()

        let workspaces = seq (desktopManager.GetDesktops())
                         |> Seq.mapi (fun i d -> { tag = i + 1; stack = None; desktop = d; layout = defaultLayout })
                         |> Seq.toList
        let (seen, unseen) = List.splitAt (List.length displayRects) workspaces
        let displays = List.mapi (fun i ((handle, _), workspace) ->
                            { n = i + 1; workspace = workspace; monitor = handle; })
                            (List.zip displayRects seen)
        { current = displays.Head; visible = displays.Tail; hidden = unseen }

    let fromCurrentAndDisplays(current: StackSet<'W, 'L>,
                               displayRects: (HMONITOR * User32.RECT) list) =
        let (seen, unseen) = List.splitAt (List.length displayRects) (current.Workspaces())
        let displays = List.mapi (fun i ((handle, _), workspace) ->
                                  { n = i + 1; workspace = workspace; monitor = handle; })
                                  (List.zip (List.ofSeq displayRects) seen)
        { current = displays.Head; visible = displays.Tail; hidden = unseen }