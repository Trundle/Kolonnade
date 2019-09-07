using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Kolonnade;

namespace KolonnadeApp
{
    public partial class MainWindow : System.Windows.Window
    {
        public ListCollectionView Selectables { get; set; }
        private string _searchText = "";
        private readonly WindowManager _windowManager = WindowManager.New();
        private readonly List<Window> _windowList;
        private const int WmHotkey = 0x0312;
        private const uint VkSpace = 0x20;

        public MainWindow()
        {
            _windowList = new List<Window>(_windowManager.GetWindows());
            Selectables = new ListCollectionView(_windowList)
            {
                Filter = SearchFilter
            };
            KeyUp += OnKeyUp;

            InitializeComponent();
            SearchInput.Focus();

            RegisterHotKey();
        }

        private bool SearchFilter(object x)
        {
            return (x as Window).Title.ToLower().Contains(_searchText);
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
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
                    _windowManager.SwitchToDesktop(e.Key - Key.D1);
                    Hide();
                    break;
                case Key.Escape:
                    Hide();
                    break;
                case Key.Enter:
                    SelectionToForeground();
                    Hide();
                    break;
                case Key.Down:
                    SelectNext();
                    break;
                case Key.Up:
                    SelectPrevious();
                    break;
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

        private void SelectionToForeground()
        {
            (SelectBox.SelectedItem as Window).ToForeground();
        }

        private void SearchInput_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = ((TextBox) sender).Text.ToLower();
            Selectables.Refresh();
            if (SelectBox.HasItems && SelectBox.SelectedIndex < 0)
            {
                SelectBox.SelectedIndex = 0;
            }
        }

        private void RegisterHotKey()
        {
            var interopHelper = new WindowInteropHelper(this);
            var hwnd = interopHelper.EnsureHandle();
            if (!RegisterHotKey(hwnd, 1, KeyModifiers.Shift | KeyModifiers.NoRepeat, VkSpace))
            {
                // XXX Handle that somehow?
                Console.WriteLine("Well that wasn't successful :( :( :('");
            }
            var hwndSource = HwndSource.FromHwnd(hwnd);
            hwndSource.AddHook(OnMessage);
        }

        private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (msg == WmHotkey)
            {
                OnHotKey();
            }
            return IntPtr.Zero;
        }

        private void OnHotKey()
        {
            Reset();
            Show();
            SearchInput.Focus();
            Activate();
        }

        private void Reset()
        {
            SearchInput.Text = "";
            _windowList.Clear();
            _windowList.AddRange(_windowManager.GetWindows());
            InvalidateVisual();
        }

        [DllImport("USER32.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RegisterHotKey(
            [In] IntPtr hWnd, [In] int id, [In] KeyModifiers fsModifiers, [In] uint vk);

        [Flags]
        private enum KeyModifiers : uint
        {
            Shift = 4u,
            NoRepeat = 0x4000u,
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void MainWindow_OnDeactivated(object sender, EventArgs e)
        {
            Hide();
        }
    }
}