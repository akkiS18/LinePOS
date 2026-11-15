using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PosElectro.Desktop.Views;

/// <summary>
/// Kodda yig'iladigan oynalar (qaytarish, qaytarishni bekor qilish) uchun maketdagi umumiy ko'rinish:
/// egasini qoplaydigan xira fon va markazdagi oq karta. Faqat ko'rinish — mantiq yo'q.
/// </summary>
internal static class ModalKit
{
    /// <summary>Oynani shaffof qoplama qilib sozlaydi va ichiga karta qo'yadi. Karta qaytariladi.</summary>
    public static Border Setup(Window w, string title, double cardWidth, bool keepScrim = false)
    {
        w.Title = title;
        w.WindowStyle = WindowStyle.None;
        w.AllowsTransparency = true;
        w.Background = Brushes.Transparent;
        w.ResizeMode = ResizeMode.NoResize;
        w.ShowInTaskbar = false;
        w.SizeToContent = SizeToContent.WidthAndHeight;
        w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        w.FontSize = 14;
        w.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PosElectro.Desktop;component/Themes/SmartControls.xaml", UriKind.Relative) });

        var root = new Grid();
        Res(root, Panel.BackgroundProperty, "Scrim");
        Res(root, TextElement.ForegroundProperty, "Ink");
        var card = new Border { Width = cardWidth, Margin = new Thickness(24), Style = (Style)w.FindResource("ModalCard") };
        root.Children.Add(card);
        w.Content = root;

        w.Loaded += (_, _) =>
        {
            var owner = w.Owner;
            if (owner == null) return;
            // Egasi o'zi ham qoplama bo'lsa, fon ikki marta xiralashmasin
            if (owner.AllowsTransparency && !keepScrim) root.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            w.SizeToContent = SizeToContent.Manual;
            if (owner.WindowState == WindowState.Maximized)
            {
                w.Left = owner.Left; w.Top = owner.Top;
                w.WindowState = WindowState.Maximized;
            }
            else if (owner.ActualWidth > w.ActualWidth && owner.ActualHeight > w.ActualHeight)
            {
                w.Left = owner.Left; w.Top = owner.Top;
                w.Width = owner.ActualWidth; w.Height = owner.ActualHeight;
            }
        };
        return card;
    }

    public static void Res(FrameworkElement e, DependencyProperty p, string key) => e.SetResourceReference(p, key);

    public static TextBlock Text(string text, double size, string brushKey, FontWeight? weight = null, bool wrap = true)
    {
        var t = new TextBlock { Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal };
        if (wrap) t.TextWrapping = TextWrapping.Wrap; else t.TextTrimming = TextTrimming.CharacterEllipsis;
        Res(t, TextBlock.ForegroundProperty, brushKey);
        return t;
    }

    public static TextBlock Label(Window w, string text) => new() { Text = text, Style = (Style)w.FindResource("FieldLabel") };

    public static Button Button(Window w, string text, string styleKey)
        => new() { Content = text, Style = (Style)w.FindResource(styleKey) };

    /// <summary>24×24 chiziqli belgi (SmartControls'dagi "Ic" bilan bir xil ko'rinish).</summary>
    public static FrameworkElement Icon(string data, string brushKey, double size)
    {
        var p = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(data), Width = 24, Height = 24, StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
        };
        Res(p, System.Windows.Shapes.Shape.StrokeProperty, brushKey);
        return new Viewbox { Width = size, Height = size, Child = p };
    }
}
