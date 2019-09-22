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