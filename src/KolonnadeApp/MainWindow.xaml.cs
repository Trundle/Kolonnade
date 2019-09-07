using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Kolonnade;

namespace KolonnadeApp
{
    public partial class MainWindow : Window
    {
        public ListCollectionView Selectables { get; set; }
        private string _searchText = "";

        public MainWindow()
        {
            KeyUp += OnKeyUp;
            Selectables = new ListCollectionView(WinUtils.windows())
            {
                Filter = x => ((WinUtils.Window) x).Title.ToLower().Contains(_searchText)
            };

            InitializeComponent();
            SearchInput.Focus();
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
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
                }
            }
        }

        private void SelectNext()
        {
            if (SelectBox.HasItems)
            {
                SelectBox.SelectedIndex = (SelectBox.SelectedIndex + 1) % SelectBox.Items.Count;
            }
        }

        private void SelectionToForeground()
        {
            ((WinUtils.Window) SelectBox.SelectedItem).ToForeground();
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
    }
}