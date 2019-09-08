﻿using System;
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

            RegisterHotKey();
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
                    var choice = e.Key - Key.D1;
                    if (string.IsNullOrEmpty(_searchText))
                    {
                        _windowManager.SwitchToDesktop(choice);
                    }
                    else
                    {
                        QuickSelect(choice);
                    }

                    ResetAndHide();
                    break;
                case Key.Escape:
                    ResetAndHide();
                    break;
                case Key.Enter:
                    SelectionToForeground();
                    ResetAndHide();
                    break;
            }
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
                (Selectables.GetItemAt(choice) as Item).Window.ToForeground();
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
            (SelectBox.SelectedItem as Item).Window.ToForeground();
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

        [Flags]
        private enum KeyModifiers : uint
        {
            Shift = 4u,
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
                .Select((w, i) =>
                {
                    var shortCut = _searchText.Length == 0 ? " " : (i + 1).ToString();
                    return new Item(shortCut, w);
                }));
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
}