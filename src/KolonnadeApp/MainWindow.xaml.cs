using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Kolonnade;

namespace KolonnadeApp
{
    public partial class MainWindow : System.Windows.Window
    {
        public ListCollectionView Selectables { get; set; }
        private string _searchText = "";
        private readonly WindowManager _windowManager = WindowManager.New();

        public MainWindow()
        {
            KeyUp += OnKeyUp;
            Selectables = new ListCollectionView(_windowManager.GetWindows().ToList())
            {
                Filter = SearchFilter
            };

            InitializeComponent();
            SearchInput.Focus();
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
    }
}