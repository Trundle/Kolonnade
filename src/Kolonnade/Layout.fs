namespace Kolonnade

open System.Drawing

/// Marker interface for messages.
type LayoutMessage = interface end

type ResizeMessage =
    // User requested a size change of the main pane
    | Expand
    | Shrink
    // User resized a window
    | WindowSizeChanged of Stack<User32.HWND> * User32.HWND * Rectangle * Rectangle
    interface LayoutMessage


type Layout =
    abstract member Description: string
    abstract member DoLayout : Stack<User32.HWND> * Rectangle -> (User32.HWND * Rectangle) list
    abstract member HandleMessage : LayoutMessage -> Layout option


/// The simplest of all layouts: take up all space, focused window at top
type Full() =
    interface Layout with
        member this.Description = "Full"

        member this.DoLayout(stack: Stack<User32.HWND>, area: Rectangle) =
            stack.ToList()
            |> List.map(fun w -> (w, area))

        member this.HandleMessage(_) = None


// The Tall layout: actual tiling 🥳
type Tall(fraction) =
    let delta = 0.05

    /// Divides the display into two rectangles with the given ration.
    let splitHorizontallyBy fraction (area : Rectangle) =
        let leftWidth = int (floor (float area.Width * fraction))
        (Rectangle(area.Left, area.Top, leftWidth, area.Height),
         Rectangle(area.Left + leftWidth, area.Top, area.Width - leftWidth, area.Height))

    /// Divides the display vertically into n sub-rectangles.
    let rec splitVertically n (area : Rectangle) =
        match n with
        | _ when n < 2 -> [area]
        | _ ->
            let smallHeight = int (float area.Height / float n)
            let remaining = Rectangle(area.Left, area.Top + smallHeight,
                                      area.Width, area.Height - smallHeight)
            Rectangle(area.Left, area.Top, area.Width, smallHeight)
            :: splitVertically (n - 1) remaining

    let handleWindowSizeChange stack hWnd (oldRect : Rectangle) (newRect : Rectangle) =
        if stack.focus = hWnd then
            // Resizing the main pane is like sending Shrink/Expand (of exact size)
            let newFract = fraction / float oldRect.Width * float newRect.Width
            Some(Tall(newFract) :> Layout)
        else None

    let handleResizeMessage = function
    | ResizeMessage.WindowSizeChanged(stack, hWnd, prev, now) -> handleWindowSizeChange stack hWnd prev now
    | ResizeMessage.Expand -> Some(Tall(min 1.0 (fraction + delta)) :> Layout)
    | ResizeMessage.Shrink -> Some(Tall(max 0.0 (fraction - delta)) :> Layout)

    /// Compute the positions for windows using a two-pane tiling algorithm.
    let tile mainPaneFraction area numberOfWindows =
        let (mainPane, otherPane) = splitHorizontallyBy mainPaneFraction area
        if numberOfWindows <= 1 then splitVertically numberOfWindows area
        else mainPane :: splitVertically (numberOfWindows - 1) otherPane

    interface Layout with
        member this.Description = "Layout"

        member this.DoLayout(stack: Stack<User32.HWND>, area: Rectangle) =
            let windows = stack.ToList()
            let rectangles = tile fraction area (List.length windows)
            List.zip windows rectangles

        member this.HandleMessage(msg) =
            match msg with
            | :? ResizeMessage as resizeMessage -> handleResizeMessage resizeMessage
            | _ -> None