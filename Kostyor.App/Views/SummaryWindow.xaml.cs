using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kostyor.App.ViewModels;

namespace Kostyor.App.Views;

/// <summary>Карточка итога встречи: копирование текста и экспорт PNG (ТЗ §4).</summary>
public partial class SummaryWindow : Window
{
    public SummaryWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SummaryViewModel vm)
                vm.SavePngRequested += SavePng;
        };
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* ignore */ }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void SavePng()
    {
        if (DataContext is not SummaryViewModel vm) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            FileName = vm.SuggestedFileName,
            Title = "Сохранить карточку встречи",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var png = RenderToPng(CardRoot, 192);
            File.WriteAllBytes(dlg.FileName, png);
        }
        catch
        {
            MessageBox.Show("Не удалось сохранить картинку.", "Костёр",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static byte[] RenderToPng(FrameworkElement element, double dpi)
    {
        var width = element.ActualWidth;
        var height = element.ActualHeight;
        var scale = dpi / 96.0;

        var rtb = new RenderTargetBitmap(
            (int)Math.Ceiling(width * scale),
            (int)Math.Ceiling(height * scale),
            dpi, dpi, PixelFormats.Pbgra32);

        // Подложка под скруглённой карточкой — тёмная, чтобы PNG не был с прозрачными углами в чатах.
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x0d, 0x0f, 0x14)), null,
                new Rect(0, 0, width, height));
            ctx.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
        }
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
