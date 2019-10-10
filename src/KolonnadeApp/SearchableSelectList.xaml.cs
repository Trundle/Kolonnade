using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace KolonnadeApp
{
    /// <summary>
    /// A keyboard-navigable selection list that can be narrowed down via a text search.
    /// </summary>
    public partial class SearchableSelectList : UserControl
    {
        public static readonly DependencyProperty SelectablesProperty = DependencyProperty.Register(
            "Selectables",
            typeof(ListCollectionView),
            typeof(SearchableSelectList),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                null,
                null
            )
        );

        public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
            "ItemTemplate",
            typeof(DataTemplate),
            typeof(SearchableSelectList),
            new FrameworkPropertyMetadata(
                default(DataTemplate),
                FrameworkPropertyMetadataOptions.AffectsRender,
                null,
                null));

        public event Action<object> Selected;
        public event Action Cancelled;
        public event Action<string> UpdateSelectables;

        public ListCollectionView Selectables
        {
            get => GetValue(SelectablesProperty) as ListCollectionView;
            set => SetValue(SelectablesProperty, value);
        }

        public DataTemplate ItemTemplate
        {
            get => GetValue(ItemTemplateProperty) as DataTemplate;
            set => SetValue(ItemTemplateProperty, value);
        }

        public SearchableSelectList()
        {
            InitializeComponent();
            SearchInput.Focus();
        }

        public new virtual void Focus()
        {
            SearchInput.Focus();
        }

        public void Reset()
        {
            SearchInput.Text = "";
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
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
                    QuickSelect(e.Key - Key.D1);
                    e.Handled = true;
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
                    QuickSelect(e.Key - Key.NumPad1);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Cancelled?.Invoke();
                    e.Handled = true;
                    break;
                case Key.Enter:
                    if (SelectBox.SelectedIndex >= 0)
                    {
                        QuickSelect(SelectBox.SelectedIndex);
                    }

                    e.Handled = true;
                    break;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
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

        private void SearchInput_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = ((TextBox) sender).Text.ToLower();
            UpdateSelectables?.Invoke(searchText);
            if (SelectBox.HasItems && SelectBox.SelectedIndex < 0)
            {
                SelectBox.SelectedIndex = 0;
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

        private void QuickSelect(in int choice)
        {
            if (choice < Selectables.Count)
            {
                Selected?.Invoke(Selectables.GetItemAt(choice));
            }
        }
    }
}