using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kolonnade;
using Window = Kolonnade.Window<System.Windows.Media.Imaging.BitmapSource>;

namespace KolonnadeApp
{
    public partial class MainWindow : System.Windows.Window
    {
        private readonly WindowManager<BitmapSource> _windowManager = WindowManager<BitmapSource>.New(IconLoader);
        private readonly List<object> _viewList = new List<object>();
        private readonly int _hotkeyMessage = RegisterWindowMessage("KolonnadeHotKey");
        private readonly Queue<(char, KeyModifiers)> _hotkeyQueue = new Queue<(char, KeyModifiers)>();
        private readonly ISelectable _windowSelectable;
        private readonly ISelectable _layoutSelectable;
        private ISelectable _selectable;
        private const int WmHotkey = 0x0312;
        private const uint VkF13 = 0x7c;

        public MainWindow()
        {
            InitializeComponent();
            _windowSelectable = new WindowSelectable(_windowManager.GetWindows, _viewList);
            _layoutSelectable = new LayoutSelectable(_windowManager, _viewList);
            _selectable = _windowSelectable;
            SelectList.Selectables = new ListCollectionView(_viewList);
            UpdateViewList("");

            RegisterHotKeys();
        }

        private void RegisterHotKeys()
        {
            var interopHelper = new WindowInteropHelper(this);
            var hWnd = interopHelper.EnsureHandle();
            // N.B. You also need to change WindowManager.ActivateViaHotkey() if you change the key here
            if (!RegisterHotKey(hWnd, 1, KeyModifiers.NoRepeat, VkF13) ||
                !RegisterHotKey(hWnd, 2, KeyModifiers.Shift | KeyModifiers.NoRepeat, VkF13))
            {
                MessageBox.Show("Could not register hotkey, exiting…");
                Application.Current.Shutdown();
            }

            var hwndSource = HwndSource.FromHwnd(hWnd);
            hwndSource.AddHook(OnMessage);
        }

        private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (msg == WmHotkey && (wparam.ToInt32() == 1 || wparam.ToInt32() == 2))
            {
                while (_hotkeyQueue.Count > 0)
                {
                    var (key, modMask) = _hotkeyQueue.Dequeue();
                    switch (modMask)
                    {
                        case 0:
                            HandleHotKey(key);
                            break;
                        case KeyModifiers.Shift:
                            HandleShiftedHotKey(key);
                            break;
                    }
                }
            }
            else if (msg == _hotkeyMessage)
            {
                _hotkeyQueue.Enqueue(((char) wparam.ToInt32(), (KeyModifiers) lparam.ToInt32()));
                _windowManager.ActivateViaHotkey();
            }

            return IntPtr.Zero;
        }

        private void HandleHotKey(char key)
        {
            switch (key)
            {
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    _windowManager.SwitchToWorkspace(key - '0');
                    break;
                case 'f':
                    OnWindowJumperHotKey();
                    break;
                // Change focus
                case 'j':
                    _windowManager.ModifyStackSet(s => s.FocusDown());
                    break;
                case 'k':
                    _windowManager.ModifyStackSet(s => s.FocusUp());
                    break;
                case 'm':
                    _windowManager.ModifyStackSet(s => s.FocusMain());
                    break;
                case '\r':
                    _windowManager.ModifyStackSet(s => s.SwapMain());
                    break;
                // Cycle layout
                case ' ':
                    _windowManager.PostMessage(ChangeLayout.NextLayout);
                    break;
                // Shrink / expand
                case 'h':
                    _windowManager.PostMessage(ResizeMessage.Shrink);
                    break;
                case 'l':
                    _windowManager.PostMessage(ResizeMessage.Expand);
                    break;
                // Focus display 1, 2, 3
                case 'w':
                case 'i':
                    _windowManager.ViewDisplay(1);
                    break;
                case 'e':
                case 'a':
                    _windowManager.ViewDisplay(2);
                    break;
                case 'r':
                    _windowManager.ViewDisplay(3);
                    break;
                // Lock screen (the key next to 'l' in Neo2 layout, as Win+l is already taken)
                case 'c':
                    LockWorkStation();
                    break;
            }
        }

        private void HandleShiftedHotKey(char key)
        {
            switch (key)
            {
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    // Note that workspaces start at 1
                    _windowManager.Shift(key - '0');
                    break;
                case 'j':
                    _windowManager.ModifyStackSet(s => s.SwapDown());
                    break;
                case 'k':
                    _windowManager.ModifyStackSet(s => s.SwapUp());
                    break;
                // Select layout
                case ' ':
                    OnLayoutJumperHotKey();
                    break;
            }
        }

        private void OnWindowJumperHotKey()
        {
            _selectable = _windowSelectable;
            SelectList.ItemTemplate = Resources["WindowListItem"] as DataTemplate;
            CommonJumperHotKey();
        }

        private void OnLayoutJumperHotKey()
        {
            _selectable = _layoutSelectable;
            SelectList.ItemTemplate = Resources["LayoutListItem"] as DataTemplate;
            CommonJumperHotKey();
        }

        private void CommonJumperHotKey()
        {
            var monitorRect = _windowManager.GetActiveMonitor();
            if (!monitorRect.IsEmpty)
            {
                Left = monitorRect.Left + (monitorRect.Width - Width) / 2.0;
                Top = monitorRect.Top + (monitorRect.Height - Height) / 2.0;
            }

            Reset();
            Show();
            Opacity = 1;
            SelectList.Focus();
            Activate();
        }

        private void Reset()
        {
            SelectList.Reset();
            _selectable.Reset();
            UpdateViewList("");
            InvalidateVisual();
        }

        [DllImport("USER32.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RegisterHotKey(
            [In] IntPtr hWnd, [In] int id, [In] KeyModifiers fsModifiers, [In] uint vk);

        [DllImport("user32.dll")]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        [Flags]
        private enum KeyModifiers : uint
        {
            Alt = 1u,
            Control = 2u,
            Shift = 4u,
            Win = 8u,
            NoRepeat = 0x4000u,
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            ResetAndHide();
        }

        private void MainWindow_OnDeactivated(object sender, EventArgs e)
        {
            if (Opacity > 0)
            {
                ResetAndHide();
            }
        }

        private void UpdateViewList(string searchText)
        {
            _selectable.UpdateSelectionItems(searchText);
            SelectList.Selectables.Refresh();
        }

        private void ResetAndHide()
        {
            // Unfortunately, WPF doesn't update the window the soon it's invisible. That means, even
            // if we reset the text input and other UI state here, WPF only updates the window once
            // it's shown again and that results in a rather ugly flickering where the old search
            // input can be seen for a split second.
            // To work around that, first "hide" the window by setting its opacity to 0, then reset
            // the UI state and in the next event loop run, finally hide the window.
            Opacity = 0;
            SelectList.Reset();
            Dispatcher.BeginInvoke(Hide, DispatcherPriority.Input);
        }

        private static BitmapSource IconLoader(IntPtr iconHandle)
        {
            return Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }

        private void SelectList_OnSelected(object item)
        {
            _selectable.OnSelected(item);
        }
    }
}