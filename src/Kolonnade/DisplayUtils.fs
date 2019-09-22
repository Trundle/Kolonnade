module internal Kolonnade.DisplayUtils

open System
open System.Collections.Generic
open System.Drawing

let rectFromMonitorHandle handle =
    let mutable monitorInfo = Unchecked.defaultof<User32.MONITORINFO>
    monitorInfo.cbSize <- sizeof<User32.MONITORINFO>
    match User32.GetMonitorInfo(handle, &monitorInfo) with
    | true -> Some(monitorInfo.rcWork)
    | false -> None

let rectangleFromMonitorHandle handle =
    rectFromMonitorHandle handle
    |> Option.map (fun rect -> Rectangle.FromLTRB(rect.top, rect.left, rect.right, rect.bottom))

let getDisplays () =
    let displays = List<IntPtr * User32.RECT>()
    let handle_monitor = User32.MONITORENUMPROC(fun hMonitor _ _ _ ->
        match rectFromMonitorHandle hMonitor with
        | Some rect -> displays.Add((hMonitor, rect))
        | None -> ()
        true)
    User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, handle_monitor, IntPtr.Zero) |> ignore
    displays