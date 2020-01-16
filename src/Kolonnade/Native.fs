namespace Kolonnade

open System
open System.Drawing
open System.Runtime.InteropServices
open System.Text

module internal Advapi32 =
    let TOKEN_QUERY = 0x8u

    type TOKEN_INFORMATION_CLASS =
        | TokenElevation = 0x14

    [<DllImport("advapi32.dll", SetLastError = true)>]
    extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, IntPtr& TokenHandle)
    [<DllImport("advapi32.dll", SetLastError = true)>]
    extern bool GetTokenInformation(IntPtr Handle, TOKEN_INFORMATION_CLASS TokenInformationClass,
                                    IntPtr TokenInformation, int TokenInformationLength, int& ReturnLength)

module internal Kernel32 =
    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool CloseHandle(IntPtr hObject)
    [<DllImport("kernel32.DLL", SetLastError = true)>]
    extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId)

module internal User32 =
    type HWND = IntPtr
    type EnumWindowsProc = delegate of HWND * int -> bool
    let PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
    let ICON_BIG = 1
    let ICON_SMALL = 0
    let ICON_SMALL2 = 2
    let WM_GETICON = 0x007F
    let GCL_HICON = -14
    let GWL_STYLE = -16
    let GWL_EXSTYLE = -20
    let MONITOR_DEFAULTTONULL = 0

    [<FlagsAttribute>]
    type WindowStyle =
        | MaximizeBox = 0x00010000
        | Child = 0x40000000

    [<FlagsAttribute>]
    type ExtendedWindowStyle =
        | ToolWindow = 0x80

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type RECT =
        { left: int
          top: int
          right: int
          bottom: int }

        member this.ToRectangle() = Rectangle.FromLTRB(this.left, this.top, this.right, this.bottom)

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type MONITORINFO =
        val mutable cbSize: int
        val rcMonitor: RECT
        val rcWork: RECT
        val dwFlags: int

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool GetKeyboardState( [<Out>] byte[] lpKeyState)

    type WinEvent =
        | Min = 1
        | Max = 0x7FFFFFFF
        | SystemForeground = 0x3
        | SystemMoveSizeStart = 0x000a
        | SystemMoveSizeEnd = 0x000b
        | ConsoleUpdateRegion = 0x4002
        | ConsoleUpdateScroll = 0x4004
        | ConsoleLayout = 0x4005
        | ObjectCreate = 0x8000
        | ObjectDestroy = 0x8001
        | ObjectShow = 0x8002
        | ObjectHide = 0x8003
        | ObjectFocus = 0x8005
        | ObjectLocationchange = 0x800b
        | ObjectNamechange = 0x800c
        | ObjectParentchange = 0x800f
        | ObjectCloaked = 0x8017
        | ObjectUncloaked = 0x8018

    type ObjId =
        | Window = 0

    type WINEVENTPROC =
       delegate of hWinEventHook:IntPtr * event:int * hwnd:HWND * idObject:int * idChild:int * idEventThread:int * dwmsEventTime:int -> Unit

    [<FlagsAttribute>]
    type WinEventFlags =
        | WINEVENT_OUTOFCONTEXT = 0
        | WINEVENT_SKIPOWNPROCESS = 2

    /// SetWindowPos flags
    [<FlagsAttribute>]
    type SwpFlags =
        | NoSize = 0x0001u
        | NoMove = 0x0002u
        | NoZOrder = 0x0004u
        | NoRedraw = 0x0008u
        | NoActivate = 0x0010u
        | ShowWindow = 0x0040u

    let HWND_BOTTOM = IntPtr(1)
    let HWND_TOP = IntPtr.Zero

    let SW_MINIMIZE = 6
    let SW_RESTORE = 9

    let DWMWA_EXTENDED_FRAME_BOUNDS = 9

    [<DllImport("user32.dll")>]
    extern bool IsIconic(HWND hWnd);
    [<DllImport("user32.dll")>]
    extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam)
    [<DllImport("user32.dll")>]
    extern bool IsWindowVisible(HWND hWnd)
    [<DllImport("user32.dll")>]
    extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount)
    [<DllImport("user32.dll")>]
    extern HWND GetShellWindow()
    [<DllImport("user32.dll")>]
    extern HWND GetDesktopWindow()
    [<DllImport("user32.dll")>]
    extern HWND GetWindowRect(HWND hWnd, RECT& lpRect)
    [<DllImport("user32.dll")>]
    extern HWND GetForegroundWindow()
    [<DllImport("user32.dll")>]
    extern bool SetForegroundWindow(HWND hWnd)
    [<DllImport("user32.dll")>]
    extern bool SetWindowPos(HWND hWnd, HWND hWndInsertAfter, int x, int y, int cx, int cy, uint32 flags)
    [<DllImport("user32.dll")>]
    extern bool ShowWindow(HWND hWnd, int nCmdShow);

    [<DllImport("user32.dll")>]
    extern uint32 GetWindowThreadProcessId(HWND hWnd, int& lpdwProcessId)
    [<DllImport("user32.dll")>]
    extern IntPtr SendMessageTimeout(HWND hWnd, int msg, IntPtr wParam, IntPtr lParam, int flags, int timeout, IntPtr& pdwResult)
    [<DllImport("user32.dll")>]
    extern bool DestroyIcon(IntPtr hIcon)
    [<DllImport("user32.dll")>]
    extern IntPtr GetClassLongPtr(HWND hWnd, int index)
    [<DllImport("user32.dll")>]
    extern IntPtr GetWindowLongPtr(HWND hWnd, int index)

    [<DllImport("user32.dll")>]
    extern IntPtr SetWinEventHook(int eventMin,
                                  int eventMax,
                                  IntPtr hmodWinEventProc,
                                  WINEVENTPROC pfnWinEventProc,
                                  int idProcess,
                                  int idThread,
                                  int dwFlags);

    // Monitor functions

    type MONITORENUMPROC =
        delegate of IntPtr * IntPtr * byref<RECT> * IntPtr -> bool

    [<DllImport("user32.dll")>]
    extern IntPtr MonitorFromWindow(HWND hWnd, int flags)

    [<DllImport("user32.dll")>]
    extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO& lpmi)

    [<DllImport("user32.dll")>]
    extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MONITORENUMPROC lpfnEnum, IntPtr dwData)

module private Dwmapi =
    [<DllImport("dwmapi.dll")>]
    extern int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, IntPtr pvAttribute, int cbAttribute)

module private Psapi =
    [<DllImport("psapi.dll", CharSet=CharSet.Unicode, SetLastError=true)>]
    extern bool GetProcessImageFileNameW(IntPtr hProcess, StringBuilder lpImageFileName, uint32 size)

    let get_process_image_file_name process_handle =
        let builder = StringBuilder(4096)
        GetProcessImageFileNameW(process_handle, builder, (uint32 builder.Capacity)) |> ignore
        builder.ToString()

module private Shcore =
    type MONITOR_DPI_TYPE = | MDT_EFFECTIVE_DPI = 0

    [<DllImport("shcore.dll")>]
    extern uint32 GetDpiForMonitor(IntPtr hMonitor, MONITOR_DPI_TYPE dpiType, uint32& dpiX, uint32& dpiY)

    [<DllImport("shcore.dll")>]
    extern int GetProcessDpiAwareness(IntPtr hprocess, int& awareness)