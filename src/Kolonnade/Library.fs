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

module VirtualDesktop =
    module internal CLSIDs =
        let ImmersiveShell = new Guid("c2f03a33-21f5-47fa-b4bb-156362a2f239")
        let VirtualDesktopManager = new Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")
        let VirtualDesktopManagerInternal = new Guid("c5e0cdca-7b6e-41b2-9fc4-d93975cc467b")

       [<ComImport;
      InterfaceType(ComInterfaceType.InterfaceIsIUnknown);
      Guid("6d5140c1-7436-11ce-8034-00aa006009fa");>]
    type internal IServiceProvider10 =
        abstract QueryService: service:byref<Guid> * rrid:byref<Guid> ->  [<MarshalAs(UnmanagedType.IUnknown)>] obj

      [<ComImport;
      InterfaceType(ComInterfaceType.InterfaceIsIUnknown);
      Guid("92ca9dcd-5622-4bba-a805-5e9f541bd8c9")>]
    type internal IObjectArray =
        abstract GetCount: unit -> uint32
        abstract GetAt: index:int * rrid:byref<Guid> *  [<MarshalAs(UnmanagedType.IUnknown)>] out:outref<obj> -> unit

      [<ComImport;
      InterfaceType(ComInterfaceType.InterfaceIsIUnknown);
      Guid("ff72ffdd-be7e-43fc-9c03-ad81681e88e4")>]
    type internal IVirtualDesktop =
        abstract IsViewVisible: obj -> bool
        abstract GetId: unit -> Guid

      [<ComImport;
      InterfaceType(ComInterfaceType.InterfaceIsIUnknown);
      Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")>]
    type internal IVirtualDesktopManager =
        abstract IsWindowOnCurrentVirtualDesktop: User32.HWND -> bool
        [<PreserveSig>]
        abstract GetWindowDesktopId: User32.HWND * outref<Guid> -> int

      [<ComImport;
      InterfaceType(ComInterfaceType.InterfaceIsIUnknown);
      Guid("f31574d6-b682-4cdc-bd56-1827860abec6")>]
    // Unfortunately the "Internal" in its name is no joke, it's not documented,
    // but see for example https://github.com/MScholtes/PSVirtualDesktop
    // and https://github.com/nathannelson97/VirtualDesktopGridSwitcher
    type internal IVirtualDesktopManagerInternal =
       abstract GetCount: unit -> int
       abstract MoveViewToDesktop: IntPtr * IVirtualDesktop -> unit
       abstract CanViewMoveDesktops: IntPtr -> unit
       abstract GetCurrentDesktop: unit -> IVirtualDesktop
       abstract GetDesktops: unit -> IObjectArray
       abstract GetAdjacentDesktop: IVirtualDesktop -> int -> IVirtualDesktop
       abstract SwitchDesktop: desktop:IVirtualDesktop -> unit
       abstract CreateDesktopW: unit -> IVirtualDesktop
       abstract RemoveDesktop: IVirtualDesktop -> IVirtualDesktop -> unit
       abstract FindDesktop: inref<Guid> -> IVirtualDesktop

    type Desktop internal (manager: IVirtualDesktopManagerInternal, desktop: IVirtualDesktop, n) =
        member this.N = n
        member this.Id = desktop.GetId()
        member this.SwitchTo() =
            manager.SwitchDesktop(desktop)

    type Manager internal (manager: IVirtualDesktopManager, managerInternal: IVirtualDesktopManagerInternal) as self =
        let desktopCache = LRUCache.OfSize<Guid, Desktop>(9)

        do
            // XXX when to refresh?
            for desktop in self.GetDesktops() do
                desktopCache.Add(desktop.Id, desktop)

        member this.GetDesktop(window: User32.HWND) =
            match manager.GetWindowDesktopId(window) with
            | (0, desktopId) when desktopId = Guid.Empty -> None
            | (0, desktopId) -> desktopCache.Get(desktopId)
            | (_, _) -> None

        member this.GetDesktops(): List<Desktop> =
            let desktops = new List<Desktop>();
            let rawDesktops = managerInternal.GetDesktops()
            for i = 0 to managerInternal.GetCount() - 1 do
                let mutable rrid = typeof<IVirtualDesktop>.GUID
                let nativeDesktop = rawDesktops.GetAt(i, &rrid)
                let desktop = new Desktop(managerInternal, nativeDesktop :?> IVirtualDesktop, i + 1) 
                desktops.Add(desktop)
            desktops

    let newManager() =
        let shell = Activator.CreateInstance(Type.GetTypeFromCLSID(CLSIDs.ImmersiveShell)) :?> IServiceProvider10
        let manager =
            Activator.CreateInstance(Type.GetTypeFromCLSID(CLSIDs.VirtualDesktopManager))
            :?> IVirtualDesktopManager
        let mutable serviceGuid = CLSIDs.VirtualDesktopManagerInternal
        let mutable riid = typeof<IVirtualDesktopManagerInternal>.GUID
        let managerInternal = shell.QueryService(&serviceGuid, &riid) :?> IVirtualDesktopManagerInternal
        new Manager(manager, managerInternal)


module internal WinUtils =
    let private window_title hwnd =
        let stringBuilder = new StringBuilder(2048)
        match User32.GetWindowText(hwnd, stringBuilder, stringBuilder.Capacity) with
        | len when len > 0 -> stringBuilder.ToString() |> Some
        | _ -> None

    let windows() =
        let shellWindow = User32.GetShellWindow()
        let windows = new List<User32.HWND * string>();
        let handle_win hwnd _ =
            if hwnd <> shellWindow && User32.IsWindowVisible(hwnd) then
                match window_title hwnd with
                | Some(title) -> windows.Add((hwnd, title))
                | None -> ()
            true
        User32.EnumWindows(new User32.EnumWindowsProc(handle_win), 0) |> ignore
        windows

type Window internal (desktop: VirtualDesktop.Desktop, hwnd: User32.HWND, title: String) =
   member this.Desktop = desktop
   member this.Title = title
   member this.ToForeground() =
       // XXX maximize if minimized?
       User32.SetForegroundWindow(hwnd)
       |> ignore

type WindowManager internal (desktopManager: VirtualDesktop.Manager) =
    let _switchToDesktop = function
    | i when i < 0 -> ()
    | i ->
        let desktops = desktopManager.GetDesktops()
        if i < desktops.Count then
            desktops.[i].SwitchTo()

    static member New () =
        WindowManager(VirtualDesktop.newManager())

    member this.GetWindows() =
        seq { for (hwnd, title) in WinUtils.windows() do
                yield (desktopManager.GetDesktop(hwnd), hwnd, title)
            }
        |> Seq.collect (function
            | (None, _, _) -> Seq.empty
            | (Some(desktop), hwnd, title) -> Seq.singleton(Window(desktop, hwnd, title))
            )

    member this.SwitchToDesktop(i) =
        _switchToDesktop i