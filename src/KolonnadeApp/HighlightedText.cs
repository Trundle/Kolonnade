using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace KolonnadeApp
{
    public class HighlightedText : TextBlock
    {
        public string Highlight
        {
            get => GetValue(HighlightProperty) as string;
            set => SetValue(HighlightProperty, value);
        }
        
        public static readonly DependencyProperty HighlightProperty = DependencyProperty.Register(
            "Highlight",
            typeof(string),
            typeof(HighlightedText),
            new FrameworkPropertyMetadata(
                "",
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnHighlightChanged,
                null
            )
        );
        
        public static readonly DependencyProperty HighlightBackgroundProperty = DependencyProperty.Register(
            "HighlightBackground",
            typeof(Brush),
            typeof(HighlightedText),
            new FrameworkPropertyMetadata(
                new SolidColorBrush(Colors.Moccasin),
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnTextInvalidated,
                null
            )
        );
        
        public static readonly DependencyProperty HighlightForegroundProperty = DependencyProperty.Register(
            "HighlightForeground",
            typeof(Brush),
            typeof(HighlightedText),
            new FrameworkPropertyMetadata(
                new SolidColorBrush(Color.FromRgb(0x2d, 0x2d, 0x2d)),
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnTextInvalidated,
                null
            )
        );

        private static void OnHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var text = d as HighlightedText;
            var newHighlight = e.NewValue as string;
            text._highlightRegex = string.IsNullOrEmpty(newHighlight) 
                ? null 
                : new Regex("(" + Regex.Escape(newHighlight) + ")", RegexOptions.IgnoreCase);
            text.Invalidate();
        }

        private static void OnTextInvalidated(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as HighlightedText).Invalidate();
        }

        private Regex _highlightRegex;

        private void Invalidate()
        {
            if (_highlightRegex != null)
            {
                FillInlines();
            }
            else
            {
                Inlines.Clear();
                Inlines.Add(new Run(Text));
            }
        }

        private void FillInlines()
        {
            var parts = _highlightRegex.Split(Text);
            Inlines.Clear();
            foreach (var part in parts)
            {
                var run = new Run(part);
                if (_highlightRegex.IsMatch(part))
                {
                    run.Background = GetValue(HighlightBackgroundProperty) as Brush;
                    run.Foreground = GetValue(HighlightForegroundProperty) as Brush;
                }
                Inlines.Add(run);
            }
        }
    }
}