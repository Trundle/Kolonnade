namespace Kolonnade

open System.Drawing

type Layout =
    abstract member DoLayout : Stack<User32.HWND> * Rectangle -> (User32.HWND * Rectangle) list


/// The simplest of all layouts: take up all space, focused window at top
type Full() =
    interface Layout with
        member this.DoLayout(stack: Stack<User32.HWND>, area: Rectangle) =
            stack.ToList()
            |> List.map(fun w -> (w, area))


// The Tall layout: actual tiling 🥳
type Tall(fraction) =
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

    /// Compute the positions for windows using a two-pane tiling algorithm.
    let tile mainPaneFraction area numberOfWindows =
        let (mainPane, otherPane) = splitHorizontallyBy mainPaneFraction area
        if numberOfWindows <= 1 then splitVertically numberOfWindows area
        else mainPane :: splitVertically (numberOfWindows - 1) otherPane

    interface Layout with
        member this.DoLayout(stack: Stack<User32.HWND>, area: Rectangle) =
            let windows = stack.ToList()
            let rectangles = tile fraction area (List.length windows)
            List.zip windows rectangles