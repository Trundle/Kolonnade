namespace Kolonnade

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Text

module private User32 =
     type HWND = IntPtr
     type EnumWindowsProc = delegate of HWND * int -> bool

     [<DllImport("USER32.DLL")>]
     extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam)
     [<DllImport("USER32.DLL")>]
     extern bool IsWindowVisible(HWND hWnd)
     [<DllImport("USER32.DLL")>]
     extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount)
     [<DllImport("USER32.DLL")>]
     extern HWND GetShellWindow()
     [<DllImport("USER32.DLL")>]
     extern bool SetForegroundWindow(HWND hWnd)


module WinUtils =
    type Window internal (hwnd: User32.HWND, title: String) =
        member this.Title = title 
        member this.ToForeground() =
            User32.SetForegroundWindow(hwnd)
            |> ignore

    let private window_title hwnd =
        let stringBuilder = new StringBuilder(2048)
        match User32.GetWindowText(hwnd, stringBuilder, stringBuilder.Capacity) with
        | len when len > 0 -> stringBuilder.ToString() |> Some
        | _ -> None

    let windows() =
        let shellWindow = User32.GetShellWindow()
        let windows = new List<Window>();
        let handle_win hwnd _ =
            if hwnd <> shellWindow && User32.IsWindowVisible(hwnd) then
                match window_title hwnd with
                | Some(title) -> windows.Add(new Window(hwnd, title))
                | None -> ()
            true
        User32.EnumWindows(new User32.EnumWindowsProc(handle_win), 0) |> ignore
        windows
