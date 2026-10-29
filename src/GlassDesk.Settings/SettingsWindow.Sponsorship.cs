using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlassDesk.Settings;

public partial class SettingsWindow
{
    private ImageSource? _alipaySponsorCode;
    private ImageSource? _wechatSponsorCode;

    private void LoadSponsorshipCodes() => SetSponsorshipCodes(
        LoadSponsorshipCode("alipay.jpg"), LoadSponsorshipCode("wechat.jpg"));

    private static BitmapFrame? LoadSponsorshipCode(string fileName)
    {
        try
        {
            using var stream = typeof(SettingsWindow).Assembly.GetManifestResourceStream(
                "GlassDesk.Settings.Sponsorship." + fileName);
            if (stream is null) return null;
            var image = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException)
        {
            // Missing or unreadable payment artwork must not prevent free use of Settings.
            return null;
        }
    }

    /// <summary>只使用本修改版维护者的收款码，缺失时不替换为其他渠道。</summary>
    public void SetSponsorshipCodes(ImageSource? alipay, ImageSource? wechat)
    {
        _alipaySponsorCode = alipay;
        _wechatSponsorCode = wechat;
    }

    private void OnSponsorClick(object sender, RoutedEventArgs e)
    {
        var useWechat = SponsorWechatOption.IsChecked == true;
        var dialog = new SponsorshipWindow(useWechat, useWechat ? _wechatSponsorCode : _alipaySponsorCode)
        { Owner = this };
        dialog.ShowDialog();
    }
}
