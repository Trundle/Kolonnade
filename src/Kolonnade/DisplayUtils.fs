module internal Kolonnade.DisplayUtils

open System
open System.Collections.Generic
open System.Drawing

let toWpfPixels handle (rect : User32.RECT) : User32.RECT =
    // WPF uses device-independent pixels, convert
    let mutable dpiX = 0u
    let mutable dpiY = 0u
    Shcore.GetDpiForMonitor(handle, Shcore.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, &dpiX, &dpiY) |> ignore
    let scaleX = 96.0 / double dpiX
    let scaleY = 96.0 / double dpiY
    let left = int (double rect.left * scaleX)
    let right = int (double rect.right * scaleX)
    let top = int (double rect.top * scaleY)
    let bottom = int (double rect.bottom * scaleY)
    { left = left; right = right; top = top; bottom = bottom; }

let rectFromMonitorHandle handle =
    let mutable monitorInfo = Unchecked.defaultof<User32.MONITORINFO>
    monitorInfo.cbSize <- sizeof<User32.MONITORINFO>
    match User32.GetMonitorInfo(handle, &monitorInfo) with
    | true -> Some(monitorInfo.rcWork)
    | false -> None

let rectangleFromMonitorHandle handle =
    rectFromMonitorHandle handle
    |> Option.map (fun rect -> Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom))

let getDisplays () =
    let displays = List<IntPtr * User32.RECT>()
    let handle_monitor = User32.MONITORENUMPROC(fun hMonitor _ _ _ ->
        match rectFromMonitorHandle hMonitor with
        | Some rect -> displays.Add((hMonitor, rect))
        | None -> ()
        true)
    User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, handle_monitor, IntPtr.Zero) |> ignore
    displays