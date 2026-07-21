using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kostyor.App.Services;

/// <summary>
/// Иконка трея/окна — .ico из ресурсов (pack-URI). H.NotifyIcon корректно конвертирует
/// именно .ico-кадр (BitmapFrame с IconBitmapDecoder), а не произвольный BitmapSource.
/// </summary>
public static class TrayIconFactory
{
    private static readonly Uri IconUri =
        new("pack://application:,,,/Assets/kostyor.ico", UriKind.Absolute);

    public static ImageSource CreateImage()
    {
        var frame = BitmapFrame.Create(IconUri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return frame;
    }
}
