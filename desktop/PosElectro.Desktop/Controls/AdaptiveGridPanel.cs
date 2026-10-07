using System;
using System.Windows;
using System.Windows.Controls;

namespace PosElectro.Desktop.Controls
{
    /// <summary>
    /// Kassa oynasidagi mahsulot kartochkalarini konteyner kengligiga moslab
    /// teng taqsimlovchi va o'ng tomonda bo'sh joy qoldirmaydigan moslashuvchan panel.
    /// </summary>
    public class AdaptiveGridPanel : Panel
    {
        public static readonly DependencyProperty MinItemWidthProperty =
            DependencyProperty.Register(nameof(MinItemWidth), typeof(double), typeof(AdaptiveGridPanel),
                new FrameworkPropertyMetadata(190.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(AdaptiveGridPanel),
                new FrameworkPropertyMetadata(172.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(AdaptiveGridPanel),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double MinItemWidth
        {
            get => (double)GetValue(MinItemWidthProperty);
            set => SetValue(MinItemWidthProperty, value);
        }

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public double Spacing
        {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (InternalChildren.Count == 0) return new Size(0, 0);

            double width = availableSize.Width;
            if (double.IsInfinity(width) || double.IsNaN(width) || width <= 0)
            {
                width = 800;
            }

            int columns = Math.Max(1, (int)Math.Floor((width + Spacing) / (MinItemWidth + Spacing)));
            double itemWidth = Math.Max(MinItemWidth, (width - (columns - 1) * Spacing) / columns);

            int rows = (int)Math.Ceiling((double)InternalChildren.Count / columns);
            double totalHeight = rows > 0 ? (rows * ItemHeight + (rows - 1) * Spacing) : 0;

            Size childSize = new Size(itemWidth, ItemHeight);
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(childSize);
            }

            return new Size(width, totalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (InternalChildren.Count == 0) return finalSize;

            double width = finalSize.Width;
            int columns = Math.Max(1, (int)Math.Floor((width + Spacing) / (MinItemWidth + Spacing)));
            double itemWidth = Math.Max(MinItemWidth, (width - (columns - 1) * Spacing) / columns);

            for (int i = 0; i < InternalChildren.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;

                double x = col * (itemWidth + Spacing);
                double y = row * (ItemHeight + Spacing);

                InternalChildren[i].Arrange(new Rect(x, y, itemWidth, ItemHeight));
            }

            return finalSize;
        }
    }
}
