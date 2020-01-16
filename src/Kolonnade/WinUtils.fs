namespace Kolonnade

open FSharp.NativeInterop
open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Text

// Some handy helpers for working with windows.

// We use nativeptr, might result in unverifiable IR code
#nowarn "9"

module internal WinUtils =
    type ElevationLevel = Elevated | NotElevated

    let isElevated token =
        let mutable dwLength = Marshal.SizeOf<int>()
        let info = Marshal.AllocHGlobal(dwLength)
        try
            Advapi32.GetTokenInformation(token, Advapi32.TOKEN_INFORMATION_CLASS.TokenElevation, info, dwLength,
                                         &dwLength) |> ignore
            Marshal.ReadInt32(info) <> 0
        finally Marshal.FreeHGlobal(info)

    let private determineProcessOwner handle =
        let mutable token = IntPtr.Zero
        if Advapi32.OpenProcessToken(handle, Advapi32.TOKEN_QUERY, &token) then
            try
                if isElevated token then Elevated else NotElevated
            finally Kernel32.CloseHandle(token) |> ignore
        else
            // Wild guess 🤷
            Elevated

    let windowTitle hWnd =
        let stringBuilder = new StringBuilder(2048)
        match User32.GetWindowText(hWnd, stringBuilder, stringBuilder.Capacity) with
        | len when len > 0 -> stringBuilder.ToString() |> Some
        | _ -> None

    let windowProcess hWnd =
        let mutable pid = 0
        User32.GetWindowThreadProcessId(hWnd, &pid) |> ignore
        match Kernel32.OpenProcess(User32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid) with
        | handle when handle <> IntPtr.Zero ->
            try
                let processName = Psapi.get_process_image_file_name(handle)
                Some (processName, determineProcessOwner handle)
            finally
                Kernel32.CloseHandle(handle) |> ignore
        | _ -> None

    let windowIcon hWnd iconLoader =
        let send_icon_msg (icon : int) =
            let mutable result = IntPtr.Zero
            match User32.SendMessageTimeout(hWnd, User32.WM_GETICON, IntPtr(icon), IntPtr.Zero,
                                            0, 50, &result) with
            | hr when hr <> IntPtr.Zero && result <> IntPtr.Zero -> Some(result)
            | _ -> None
        let icon_from_class_ptr () =
            match User32.GetClassLongPtr(hWnd, User32.GCL_HICON) with
            | handle when handle <> IntPtr.Zero -> Some handle
            | _ -> None
        let load_icon handle =
            try
                iconLoader handle
            finally
                User32.DestroyIcon(handle) |> ignore
        send_icon_msg User32.ICON_BIG
        |> Option.orElseWith (fun _ -> send_icon_msg User32.ICON_SMALL2)
        |> Option.orElseWith icon_from_class_ptr
        |> Option.map load_icon

    let windows predicate =
        let result = new List<User32.HWND>();
        let handleWin hWnd _ =
            if predicate hWnd then result.Add(hWnd)
            true
        User32.EnumWindows(User32.EnumWindowsProc(handleWin), 0) |> ignore
        result

    /// Returns the actual window rect, without DWM's frame.
    let windowFrameRect hWnd =
        let rect: User32.RECT = { left = 0; right = 0; top = 0; bottom = 0 }
        Dwmapi.DwmGetWindowAttribute(hWnd, User32.DWMWA_EXTENDED_FRAME_BOUNDS,
                                     IntPtr(NativePtr.toVoidPtr &&rect), Marshal.SizeOf<User32.RECT>())
        |> ignore
        rect