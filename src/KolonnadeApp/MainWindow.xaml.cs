using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kolonnade;
using Window = Kolonnade.Window<System.Windows.Media.Imaging.BitmapSource>;

namespace KolonnadeApp
{
    public partial class MainWindow : System.Windows.Window
    {
        public ListCollectionView Selectables { get; set; }
        private string _searchText = "";
        private readonly WindowManager<BitmapSource> _windowManager = WindowManager<BitmapSource>.New(IconLoader);
        private readonly List<Window> _windowList;
        private readonly List<Item> _viewList;
        private readonly History _history = new History(16);
        private readonly int _hotkeyMessage = RegisterWindowMessage("KolonnadeHotKey");
        private const int WmHotkey = 0x0312;
        private const uint VkSpace = 0x20;

        public MainWindow()
        {
            _windowList = new List<Window>(_windowManager.GetWindows());
            _viewList = new List<Item>();
            Selectables = new ListCollectionView(_viewList);
            UpdateViewList();

            InitializeComponent();
            SearchInput.Focus();

            RegisterHotKeys();
        }

        private bool SearchFilter(Window w)
        {
            return w.Title.ToLower().Contains(_searchText)
                   || (w.Process != null && w.Process.ToLower().Contains(_searchText));
        }

        private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.D1:
                case Key.D2:
                case Key.D3:
                case Key.D4:
                case Key.D5:
                case Key.D6:
                case Key.D7:
                case Key.D8:
                case Key.D9:
                    HandleNumberPress(e.Key - Key.D1);
                    break;
                case Key.NumPad1:
                case Key.NumPad2:
                case Key.NumPad3:
                case Key.NumPad4:
                case Key.NumPad5:
                case Key.NumPad6:
                case Key.NumPad7:
                case Key.NumPad8:
                case Key.NumPad9:
                    HandleNumberPress(e.Key - Key.NumPad1);
                    break;
                case Key.Escape:
                    ResetAndHide();
                    break;
                case Key.Enter:
                    if (SelectBox.SelectedIndex >= 0)
                    {
                        ToForeground(SelectBox.SelectedIndex);
                    }

                    ResetAndHide();
                    break;
            }
        }

        private void HandleNumberPress(int choice)
        {
            if (string.IsNullOrEmpty(_searchText))
            {
                _windowManager.SwitchToDesktop(choice);
            }
            else
            {
                QuickSelect(choice);
            }

            ResetAndHide();
        }

        private void MainWindow_OnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Down:
                    SelectNext();
                    break;
                case Key.Up:
                    SelectPrevious();
                    break;
            }
        }

        private void QuickSelect(in int choice)
        {
            if (choice < Selectables.Count)
            {
                ToForeground(choice);
            }
        }

        private void SelectPrevious()
        {
            if (SelectBox.HasItems)
            {
                if (SelectBox.SelectedIndex > 0)
                {
                    SelectBox.SelectedIndex -= 1;
                }
                else
                {
                    SelectBox.SelectedIndex = SelectBox.Items.Count - 1;
                    SelectBox.ScrollIntoView(SelectBox.SelectedItem);
                }
            }
        }

        private void SelectNext()
        {
            if (SelectBox.HasItems)
            {
                SelectBox.SelectedIndex = (SelectBox.SelectedIndex + 1) % SelectBox.Items.Count;
                SelectBox.ScrollIntoView(SelectBox.SelectedItem);
            }
        }

        private void ToForeground(int index)
        {
            var window = (Selectables.GetItemAt(index) as Item).Window;
            _history.Append(window);
            window.ToForeground();
        }

        private void SearchInput_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = ((TextBox) sender).Text.ToLower();
            UpdateViewList();
            if (SelectBox.HasItems && SelectBox.SelectedIndex < 0)
            {
                SelectBox.SelectedIndex = 0;
            }
        }

        private void RegisterHotKeys()
        {
            var interopHelper = new WindowInteropHelper(this);
            var hWnd = interopHelper.EnsureHandle();
            if (!RegisterHotKey(hWnd, 1, KeyModifiers.Shift | KeyModifiers.NoRepeat, VkSpace))
            {
                // XXX Handle that somehow?
                Console.WriteLine("Well that wasn't successful :( :( :('");
            }

            var hwndSource = HwndSource.FromHwnd(hWnd);
            hwndSource.AddHook(OnMessage);
        }

        private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (msg == WmHotkey && wparam.ToInt32() == 1)
            {
                OnJumperHotKey();
            }
            else if (msg == _hotkeyMessage)
            {
                var key = (char) wparam.ToInt32();
                switch ((KeyModifiers) lparam.ToInt32())
                {
                    case 0:
                        HandleHotKey(key);
                        break;
                    case KeyModifiers.Shift:
                        HandleShiftedHotKey(key);
                        break;
                }
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
                    _windowManager.SwitchToDesktop(key - '1');
                    break;
                case 'j':
                    _windowManager.FocusDown();
                    break;
                case 'k':
                    _windowManager.FocusUp();
                    break;
                case 'm':
                    _windowManager.FocusMain();
                    break;
                case '\r':
                    _windowManager.RaiseToMain();
                    break;
                case ' ':
                    _windowManager.CycleLayout();
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
            }
        }

        private void OnJumperHotKey()
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
            SearchInput.Focus();
            Activate();
        }

        private void Reset()
        {
            SearchInput.Text = "";
            // XXX this could replace the list now
            _windowList.Clear();
            _windowList.AddRange(_windowManager.GetWindows());
            UpdateViewList();
            InvalidateVisual();
        }

        [DllImport("USER32.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RegisterHotKey(
            [In] IntPtr hWnd, [In] int id, [In] KeyModifiers fsModifiers, [In] uint vk);

        [DllImport("user32.dll")]
        private static extern int RegisterWindowMessage(string lpString);

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

        private void UpdateViewList()
        {
            _viewList.Clear();
            _viewList.AddRange(_windowList
                .Where(SearchFilter)
                .OrderBy(x => x, _history.Comparer)
                .Select((w, i) =>
                {
                    var shortCut = _searchText.Length == 0 ? " " : (i + 1).ToString();
                    return new Item(shortCut, w);
                })
            );
            Selectables.Refresh();
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
            SearchInput.Text = string.Empty;
            Dispatcher.BeginInvoke(Hide, DispatcherPriority.Input);
        }

        private static BitmapSource IconLoader(IntPtr iconHandle)
        {
            return Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
    }

    class Item
    {
        public string ShortCut { get; }
        public Window Window { get; }

        public Item(string shortCut, Window window)
        {
            ShortCut = shortCut;
            Window = window;
        }
    }

    class History
    {
        public int MaxSize { get; }
        private readonly LinkedList<Id> _history = new LinkedList<Id>();

        public History(int maxSize)
        {
            MaxSize = maxSize;
        }

        public IComparer<Window> Comparer
        {
            get => new HistoryComparer(() => _history);
        }

        public void Append(Window value)
        {
            _history.Remove(value.Id);
            _history.AddFirst(value.Id);
            if (_history.Count > MaxSize)
            {
                _history.RemoveLast();
            }
        }

        class HistoryComparer : IComparer<Window>
        {
            private readonly Func<LinkedList<Id>> _history;

            public HistoryComparer(Func<LinkedList<Id>> history)
            {
                _history = history;
            }

            public int Compare(Window x, Window y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }

                if (x == null)
                {
                    return -1;
                }

                if (y == null)
                {
                    return 1;
                }

                var yIndex = _history().TakeWhile(e => !e.Equals(y.Id)).Count();
                var xIndex = _history().TakeWhile(e => !e.Equals(x.Id)).Count();

                // It's rather unlikely that one wants to switch to the same window again,
                // hence swap first and second window
                if (xIndex == 0 && yIndex == 1)
                {
                    return 1;
                }

                if (yIndex == 0 && xIndex == 1)
                {
                    return -1;
                }

                return xIndex.CompareTo(yIndex);
            }
        }
    }
}