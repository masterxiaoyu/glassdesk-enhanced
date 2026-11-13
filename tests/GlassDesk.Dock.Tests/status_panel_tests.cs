using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GlassDesk.App.TopBar;
using GlassDesk.AppServices;
using GlassDesk.Core.Storage;
using GlassDesk.Platform.Shell;

namespace GlassDesk.Dock.Tests;

public sealed class StatusPanelTests
{
  [Theory]
  [InlineData(96)]
  [InlineData(144)]
  [InlineData(192)]
  public void 托盘图标在通用按钮主题下完整绘制(double dpi)
  {
    StaTest.Run(() =>
    {
      using var bridge = new SystemTrayBridge();
      var items = (Dictionary<(nint, uint), SystemTrayItem>)typeof(
        SystemTrayBridge).GetField("items_", BindingFlags.Instance
          | BindingFlags.NonPublic)!.GetValue(bridge)!;
      items[(0, 1)] = new SystemTrayItem(0, 1, Guid.Empty, 0, 0,
        "完整图标", false, new ExtractedIcon(16, 16,
          Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray()));
      var dock = new DockWindow { SystemTray = bridge };
      dock.Resources.MergedDictionaries.Add(new ResourceDictionary
      {
        Source = new Uri("pack://application:,,,/GlassDesk.UI;component/"
          + "Theming/LiquidGlassTheme.xaml"),
      });
      try
      {
        dock.Show();
        dock.OpenSystemTray((Button)dock.FindName("ThisPcButton"));
        Dispatcher.CurrentDispatcher.Invoke(() => { },
          DispatcherPriority.ApplicationIdle);
        var popup = (Popup)typeof(DockWindow).GetField("tray_popup_",
          BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dock)!;
        var border = (Border)popup.Child;
        border.UpdateLayout();
        var scroll = (ScrollViewer)border.Child;
        var content = (StackPanel)scroll.Content;
        var icons = content.Children.OfType<WrapPanel>().Single();
        var button = Assert.IsAssignableFrom<Button>(icons.Children[0]);
        var icon = Assert.IsType<Image>(button.Content);
        var start = icon.TranslatePoint(new Point(), button);
        Assert.Equal(16, icon.ActualWidth);
        Assert.Equal(16, icon.ActualHeight);
        Assert.True(start.X >= 0 && start.X + 16 <= button.ActualWidth);
        Assert.True(start.Y >= 0 && start.Y + 16 <= button.ActualHeight);
        var size = (int)Math.Ceiling(button.ActualWidth * dpi / 96);
        var image = new RenderTargetBitmap(size, size, dpi, dpi,
          PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
          context.DrawRectangle(new VisualBrush(button), null,
            new Rect(0, 0, button.ActualWidth, button.ActualHeight));
        image.Render(drawing);
        var pixels = new byte[size * size * 4];
        image.CopyPixels(pixels, size * 4, 0);
        // 检查图标四角的实际像素，避免只校验 Width 却漏掉模板裁切。
        foreach (var dx in new[] { 1.0, 14.0 })
        foreach (var dy in new[] { 1.0, 14.0 })
        {
          var x = (int)((start.X + dx) * dpi / 96);
          var y = (int)((start.Y + dy) * dpi / 96);
          Assert.Equal(255, pixels[(y * size + x) * 4 + 3]);
        }
        popup.IsOpen = false;
      }
      finally { dock.Close(); }
    });
  }

  [Fact]
  public void 网络音量日历独立展开并在切换时释放音频会话()
  {
    StaTest.Run(() =>
    {
      var root = Path.Combine(Path.GetTempPath(),
        "GlassDesk-status-panels-" + Guid.NewGuid().ToString("N"));
      var data = new GlassDeskDataStore(new AppPaths(root));
      data.LoadAll();
      using var services = new AppServiceHost(data);
      var bar = new TopBarWindow(services);
      bar.Resources.MergedDictionaries.Add(new ResourceDictionary
      {
        Source = new Uri("pack://application:,,,/GlassDesk.UI;component/"
          + "Theming/LiquidGlassTheme.xaml"),
      });
      try
      {
        bar.SetEnabled(true);
        bar.UpdateLayout();
        Assert.Equal(36, bar.ActualHeight);
        RenderElement((FrameworkElement)bar.Content, "menu-bar");
        var network = (Popup)bar.FindName("NetworkPopup");
        var volume = (Popup)bar.FindName("VolumePopup");
        var calendar = (Popup)bar.FindName("CalendarPopup");
        ((Button)bar.FindName("NetworkButton")).RaiseEvent(
          new RoutedEventArgs(Button.ClickEvent));
        Assert.True(network.IsOpen);
        Assert.False(volume.IsOpen);
        RenderPanel(network, "network");
        ((Button)bar.FindName("VolumeButton")).RaiseEvent(
          new RoutedEventArgs(Button.ClickEvent));
        Assert.True(volume.IsOpen);
        Assert.False(network.IsOpen);
        RenderPanel(volume, "volume");
        ((Button)bar.FindName("CalendarButton")).RaiseEvent(
          new RoutedEventArgs(Button.ClickEvent));
        Assert.True(calendar.IsOpen);
        Assert.False(volume.IsOpen);
        Dispatcher.CurrentDispatcher.Invoke(() => { },
          DispatcherPriority.ApplicationIdle);
        var days = (UniformGrid)bar.FindName("MonthCalendar");
        Assert.Equal(42, days.Children.Count);
        Assert.Contains(days.Children.OfType<Button>(), day =>
          Equals(day.Tag, DateTime.Today));
        RenderPanel(calendar, "calendar");
        Assert.Null(typeof(TopBarWindow).GetField("volume_",
          BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar));
        ((Button)bar.FindName("ControlCenterButton")).RaiseEvent(
          new RoutedEventArgs(Button.ClickEvent));
        var controls = (Popup)bar.FindName("ControlCenterPopup");
        Assert.True(controls.IsOpen);
        Assert.False(calendar.IsOpen);
        RenderPanel(controls, "control-center");
        var battery_button = (Button)bar.FindName("BatteryButton");
        battery_button.Tag = false;
        Assert.Equal(Visibility.Collapsed,
          ((FrameworkElement)bar.FindName("BatteryPowerMark")).Visibility);
        Assert.Equal(Visibility.Visible,
          ((FrameworkElement)bar.FindName("BatteryFill")).Visibility);
        RenderElement(battery_button, "battery-unplugged");
        battery_button.Tag = true;
        Assert.Equal(Visibility.Visible,
          ((FrameworkElement)bar.FindName("BatteryPowerMark")).Visibility);
        Assert.Equal(Visibility.Collapsed,
          ((FrameworkElement)bar.FindName("BatteryFill")).Visibility);
        RenderElement(battery_button, "battery-powered");
        battery_button.RaiseEvent(
          new RoutedEventArgs(Button.ClickEvent));
        var battery = (Popup)bar.FindName("BatteryPopup");
        Assert.True(battery.IsOpen);
        Assert.False(controls.IsOpen);
        Assert.NotEmpty(((TextBlock)bar.FindName("BatterySummary")).Text);
        RenderPanel(battery, "battery");
        ((Button)bar.FindName("NotificationsButton")).RaiseEvent(
          new RoutedEventArgs(Button.ClickEvent));
        var notifications = (Popup)bar.FindName("NotificationsPopup");
        Assert.True(notifications.IsOpen);
        Assert.False(battery.IsOpen);
        // 测试宿主没有应用包标识，只验证授权入口，不读取用户通知内容。
        Assert.False(SystemNotifications.HasIdentity);
        Assert.Equal(Visibility.Collapsed,
          ((Button)bar.FindName("NotificationAccessButton")).Visibility);
        Assert.Equal("系统通知同步暂未开放。",
          ((TextBlock)bar.FindName("NotificationStatus")).Text);
        RenderPanel(notifications, "notifications");
        bar.SetEnabled(false);
        Assert.False(calendar.IsOpen);
        Assert.False(controls.IsOpen);
        Assert.False(battery.IsOpen);
        Assert.False(notifications.IsOpen);
      }
      finally
      {
        bar.Close();
        Directory.Delete(root, true);
      }
    });
  }

  private static void RenderPanel(Popup panel, string name)
    => RenderElement((FrameworkElement)panel.Child, name);

  private static void RenderElement(FrameworkElement child, string name)
  {
    Dispatcher.CurrentDispatcher.Invoke(() => { },
      DispatcherPriority.ApplicationIdle);
    child.UpdateLayout();
    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(child.ActualWidth),
      (int)Math.Ceiling(child.ActualHeight), 96, 96, PixelFormats.Pbgra32);
    var drawing = new DrawingVisual();
    using (var context = drawing.RenderOpen())
      context.DrawRectangle(new VisualBrush(child), null,
        new Rect(0, 0, child.ActualWidth, child.ActualHeight));
    bitmap.Render(drawing);
    var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
      "../../../../../artifacts/status-panel-validation"));
    Directory.CreateDirectory(folder);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var file = File.Create(Path.Combine(folder, name + ".png"));
    encoder.Save(file);
  }

  [Fact]
  public void 系统音量读写原值并释放设备引用()
  {
    StaTest.Run(() =>
    {
      using var volume = SystemVolume.Open();
      if (volume is null) return; // 没有播放设备时界面提供禁用状态。
      Assert.True(volume.TryRead(out var level, out var muted));
      Assert.InRange(level, 0f, 1f);
      // 使用原值验证原生调用，不改变用户当前音量和静音状态。
      Assert.True(volume.SetLevel(level));
      Assert.True(volume.SetMuted(muted));
      Assert.True(volume.TryRead(out var actual, out var actualMuted));
      Assert.Equal(level, actual);
      Assert.Equal(muted, actualMuted);
      volume.Dispose();
      Assert.False(volume.TryRead(out _, out _));
    });
  }
}
