using System.Windows;
using System.Windows.Media;

namespace KolonnadeApp
{
    // XXX Allow in-line highlighting (e.g. bold subtext)?
    class OutlinedText : FrameworkElement
    {
        // Represents the actual text
        private Geometry _textGeometry;

        /// <summary>
        /// Invoked when a dependency property has changed. Generates a new FormattedText object to display.
        /// </summary>
        /// <param name="d">OutlineText object whose property was updated.</param>
        /// <param name="e">Event arguments for the dependency property.</param>
        private static void OnOutlinedTextInvalidated(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((OutlinedText) d).CreateText();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.DrawGeometry(Fill, new Pen(Stroke, StrokeThickness), _textGeometry);
        }

        private void CreateText()
        {
            var fontStyle = FontStyles.Normal;
            var fontWeight = FontWeights.Medium;

            var formattedText = new FormattedText(
                Text,
                System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
                FlowDirection.LeftToRight,
                new Typeface(
                    Font,
                    fontStyle,
                    fontWeight,
                    FontStretches.Normal),
                FontSize,
                // Will be ignored anyway
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            );
            _textGeometry = formattedText.BuildGeometry(new Point(0, 0));
        }

        #region Properties

        public Brush Fill
        {
            get => (Brush) GetValue(FillProperty);

            set => SetValue(FillProperty, value);
        }

        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            "Fill",
            typeof(System.Windows.Media.Brush),
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(
                new SolidColorBrush(Colors.LightSteelBlue),
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnOutlinedTextInvalidated,
                null
            )
        );

        public Brush Stroke
        {
            get => (Brush) GetValue(StrokeProperty);

            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
            "Stroke",
            typeof(System.Windows.Media.Brush),
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(
                new SolidColorBrush(Colors.Teal),
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnOutlinedTextInvalidated,
                null
            )
        );

        public double StrokeThickness
        {
            get => (double) GetValue(StrokeThicknessProperty);

            set => SetValue(StrokeThicknessProperty, value);
        }

        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            "StrokeThickness",
            typeof(double),
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(
                0.0,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnOutlinedTextInvalidated,
                null
            )
        );
        
        /// <summary>
        /// The font to use for the displayed formatted text.
        /// </summary>
        public FontFamily Font
        {
            get => (FontFamily) GetValue(FontProperty);

            set => SetValue(FontProperty, value);
        }

        /// <summary>
        /// Identifies the Font dependency property.
        /// </summary>
        public static readonly DependencyProperty FontProperty = DependencyProperty.Register(
            "Font",
            typeof(System.Windows.Media.FontFamily),
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(
                new System.Windows.Media.FontFamily("Arial"),
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnOutlinedTextInvalidated,
                null
            )
        );

        public double FontSize
        {
            get => (double) GetValue(FontSizeProperty);

            set => SetValue(FontSizeProperty, value);
        }

        /// <summary>
        /// Identifies the FontSize dependency property.
        /// </summary>
        public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
            "FontSize",
            typeof(double),
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(
                48.0,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnOutlinedTextInvalidated,
                null
            )
        );

        /// <summary>
        /// The text to display.
        /// </summary>
        public string Text
        {
            get => (string) GetValue(TextProperty);

            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text",
            typeof(string),
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(
                "",
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnOutlinedTextInvalidated,
                null
            )
        );

        #endregion
    }
}