using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GlassDesk.App.TopBar;
using GlassDesk.AppServices;
using GlassDesk.Core.Models;
using GlassDesk.Core.Storage;
using GlassDesk.Launcher;
using GlassDesk.Settings;
using GlassDesk.UI.Controls;
using GlassDesk.UI.Settings;
using GlassDesk.UI.Theming;

namespace GlassDesk.Dock.Tests;

/// <summary>用隔离演示数据导出真实控件，不采集用户桌面和通知。</summary>
public sealed class MarketingPreviewTests
{
  [Fact]
  public void 导出产品宣传界面素材()
  {
    if (Environment.GetEnvironmentVariable("GLASSDESK_MARKETING_EXPORT") != "1")
      return;
    StaTest.Run(() =>
    {
      var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "../../../../../artifacts/marketing-ui"));
      Directory.CreateDirectory(output);
      var root = Path.Combine(Path.GetTempPath(),
        "GlassDesk-marketing-" + Guid.NewGuid().ToString("N"));
      using var theme = ThemeService.Initialize(Dispatcher.CurrentDispatcher);
      var data = new GlassDeskDataStore(new AppPaths(root));
      data.LoadAll();
      data.Settings.Current.Appearance.ReduceMotion = true;
      data.Settings.Current.Appearance.DockBuiltinIconBrightness = 1;
      data.Settings.Current.Appearance.LaunchpadIconFilled = true;
      data.Settings.Current.Dock.ShowRunningWindows = false;
      data.Settings.Current.Dock.ShowTopBar = true;
      data.Settings.Current.Launcher.Rows = 3;
      data.Settings.Current.Launcher.Columns = 4;
      theme.Update(data.Settings.Current.Appearance);
      var windows = Environment.GetFolderPath(
        Environment.SpecialFolder.Windows);
      var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
      var programs = new[]
      {
        ("Edge", @"C:\Program Files (x86)\Microsoft\Edge\Application"
          + @"\msedge.exe"),
        ("Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe"),
        ("文件资源管理器", Path.Combine(windows, "explorer.exe")),
        ("记事本", Path.Combine(system, "notepad.exe")),
        ("计算器", Path.Combine(system, "calc.exe")),
        ("系统设置", Path.Combine(system, "control.exe")),
        ("任务管理器", Path.Combine(system, "Taskmgr.exe")),
        ("PowerShell", Path.Combine(system,
          "WindowsPowerShell/v1.0/powershell.exe")),
        ("系统信息", Path.Combine(system, "msinfo32.exe")),
        ("远程桌面", Path.Combine(system, "mstsc.exe")),
      };
      foreach (var (name, path) in programs.Where(item =>
          File.Exists(item.Item2)))
        data.Library.Add(new AppEntry
        {
          Id = "demo-" + data.Library.Items.Count,
          DisplayName = name, Target = path, IconSource = path,
        });
      data.Library.Save();
      var ids = data.Library.Items.Select(item => item.Id).ToList();
      data.Layouts.Dock.PinnedItemIds = ids.Take(6).ToList();
      data.Layouts.Dock.ToolGroups =
      [
        new DockToolGroup
        {
          Id = "demo-tools", Name = "常用工具", ItemIds = ids.Skip(6).ToList(),
        },
      ];
      data.Layouts.Dock.PinnedItemIds.Add("stack:demo-tools");
      data.Layouts.SaveDock();
      var launcher_layout = new LauncherLayout
      {
        SchemaVersion = 2,
        Pages =
        [
          new LauncherPage
          {
            Id = "demo-page",
            Slots = ids.Take(8).Select(id => new LauncherSlot
              { ItemId = id }).Append(new LauncherSlot
              {
                Kind = LauncherSlotKind.Group,
                Group = new LauncherGroup
                {
                  Id = "demo-group", Name = "常用工具",
                  ItemIds = ids.Skip(6).ToList(),
                },
              }).ToList(),
          },
        ],
      };
      Assert.True(data.Layouts.CommitLauncher(launcher_layout,
        data.Layouts.LauncherVersion));
      using var services = new AppServiceHost(data);
      foreach (var item in data.Library.Items)
        services.Icons.EnsureFor(item.IconSource);
      var dock = new DockWindow();
      var launcher = new LauncherWindow();
      var settings = new SettingsWindow();
      var bar = new TopBarWindow(services);
      try
      {
        foreach (var window in new Window[] { dock, launcher, settings, bar })
        {
          window.Resources.MergedDictionaries.Add(new ResourceDictionary
          {
            Source = new Uri("pack://application:,,,/GlassDesk.UI;component/"
              + "Theming/LiquidGlassTheme.xaml"),
          });
          _ = new WindowInteropHelper(window).EnsureHandle();
        }
        var host = new DockSettingsHost(Dispatcher.CurrentDispatcher);
        host.Update(data.Settings.Current.Dock);
        dock.AttachServices(services, host, () => { });
        var dock_content = (FrameworkElement)dock.FindName("DockContentLayer");
        Layout(dock_content, new Size(1200, 90));
        // 用实际内容期望尺寸裁切，不含窗口外的桌面空白。
        Layout(dock_content, dock_content.DesiredSize);
        Export(dock_content, output, "dock", transparent: true);
        ((LiquidGlassSurface)dock.FindName("GlassSurface")).IsBackdropActive
          = () => false;
        var dock_root = (FrameworkElement)dock.Content;
        dock_root.Measure(new Size(double.PositiveInfinity,
          double.PositiveInfinity));
        Layout(dock_root, dock_root.DesiredSize);
        Export(dock_root, output, "dock-glass", transparent: true);
        ContextMenu? stack_menu = null;
        EventManager.RegisterClassHandler(typeof(ContextMenu),
          ContextMenu.OpenedEvent, new RoutedEventHandler((sender, _) =>
          {
            stack_menu = (ContextMenu)sender;
            stack_menu.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
              Source = new Uri("pack://application:,,,/GlassDesk.UI;component/"
                + "Theming/LiquidGlassTheme.xaml"),
            });
            stack_menu.Style = (Style)stack_menu.FindResource(
              typeof(ContextMenu));
          }));
        typeof(DockWindow).GetMethod("ShowStack",
          BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dock,
            [data.Layouts.Dock.ToolGroups[0], false]);
        Flush();
        Assert.NotNull(stack_menu);
        stack_menu.UpdateLayout();
        Export(stack_menu, output, "dock-stack", transparent: true);
        stack_menu.IsOpen = false;

        launcher.AttachServices(services, true);
        launcher.ApplySettings(data.Settings.Current.Launcher);
        var launcher_panel = (FrameworkElement)launcher.FindName(
          "LauncherPanelRoot");
        launcher_panel.Width = 1000;
        launcher_panel.Height = 650;
        ((LiquidGlassSurface)launcher.FindName("GlassSurface")).SetPanelShape(
          new Rect(0, 0, 1000, 650), 80);
        var launcher_content = (FrameworkElement)launcher.Content;
        Layout(launcher_content, new Size(1000, 650));
        typeof(LauncherWindow).GetMethod("RenderTiles",
          BindingFlags.Instance | BindingFlags.NonPublic)!
          .Invoke(launcher, [false, false]);
        Flush();
        Layout(launcher_content, new Size(1000, 650));
        Flush();
        Export(launcher_content, output, "launcher");

        settings.AttachDataStore(data, null);
        settings.Width = 980;
        settings.Height = 780;
        var settings_content = (FrameworkElement)settings.Content;
        settings_content.Width = 980;
        settings_content.Height = 780;
        Layout(settings_content, new Size(980, 780));
        ((ListBox)settings.FindName("SettingsPageList")).SelectedIndex = 0;
        var scroll = (ScrollViewer)settings.FindName("SettingsScroll");
        scroll.ScrollToTop();
        Flush();
        Export(settings_content, output, "settings-appearance");
        var heading = (FrameworkElement)settings.FindName("DockSectionHeading");
        scroll.ScrollToVerticalOffset(heading.TranslatePoint(new Point(),
          (UIElement)scroll.Content).Y);
        ((ListBox)settings.FindName("SettingsPageList")).SelectedIndex = 1;
        Flush();
        Layout(settings_content, new Size(980, 780));
        Export(settings_content, output, "settings-dock");

        var bar_content = (FrameworkElement)bar.Content;
        ((TextBlock)bar.FindName("ApplicationName")).Text = "桌面";
        ((TextBlock)bar.FindName("Clock")).Text = "11月12日 周四 21:30";
        Invoke(bar, "RefreshNetworkStatus");
        Invoke(bar, "RefreshBatteryStatus");
        Layout(bar_content, new Size(1440, 36));
        Export(bar_content, output, "menu-bar", transparent: true);
        ((Slider)bar.FindName("VolumeSlider")).Value = 65;
        ((TextBlock)bar.FindName("VolumeReadout")).Text = "65%";
        foreach (var (name, file) in new[]
          { ("NetworkPopup", "network"), ("VolumePopup", "volume"),
            ("BatteryPopup", "battery"),
            ("ControlCenterPopup", "control-center") })
        {
          var panel = (FrameworkElement)((Popup)bar.FindName(name)).Child;
          panel.Measure(new Size(double.PositiveInfinity,
            double.PositiveInfinity));
          Layout(panel, panel.DesiredSize);
          Export(panel, output, file, transparent: true);
        }
        var today = new DateTime(2026, 11, 12);
        typeof(TopBarWindow).GetField("calendar_month_",
          BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(bar,
            new DateTime(2026, 11, 1));
        typeof(TopBarWindow).GetField("calendar_selected_",
          BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(bar, today);
        ((TextBlock)bar.FindName("CalendarHeading")).Text = "11月12日 星期四";
        foreach (var day in "日一二三四五六")
          ((UniformGrid)bar.FindName("WeekdayHeader")).Children.Add(
            new TextBlock
            {
              Text = day.ToString(), FontSize = 11, Opacity = .45,
              HorizontalAlignment = HorizontalAlignment.Center,
            });
        Invoke(bar, "RenderCalendarMonth");
        var calendar = (FrameworkElement)((Popup)bar.FindName(
          "CalendarPopup")).Child;
        calendar.Measure(new Size(double.PositiveInfinity,
          double.PositiveInfinity));
        Layout(calendar, calendar.DesiredSize);
        Export(calendar, output, "calendar", transparent: true);
        File.WriteAllText(Path.Combine(output, "SOURCES.txt"),
          "真实 WPF 控件离线渲染；未接管系统任务栏。\n"
          + "Dock收纳菜单调用真实ShowStack后即关闭，未点击或启动应用。\n"
          + "应用列表为隔离演示数据；图标来自本机公开程序文件资源。\n"
          + "音量65%和日期为演示值，不改变系统音量。\n"
          + "没有读取用户通知、桌面、个人文件或登录信息。\n"
          + "未呈现真实桌面采样折射，海报可配合背景排版。\n");
      }
      finally
      {
        bar.Close(); settings.Close(); launcher.Close(); dock.Close();
        if (Path.GetDirectoryName(root) !=
            Path.TrimEndingDirectorySeparator(Path.GetTempPath()))
          throw new IOException("Unexpected marketing data cleanup boundary.");
        Directory.Delete(root, true);
      }
    });
  }

  private static void Invoke(object target, string method)
    => target.GetType().GetMethod(method,
      BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);

  private static void Flush()
    => Dispatcher.CurrentDispatcher.Invoke(() => { },
      DispatcherPriority.SystemIdle);

  private static void Layout(FrameworkElement element, Size size)
  {
    element.Measure(size);
    element.Arrange(new Rect(new Point(), size));
    element.UpdateLayout();
    Flush();
  }

  private static void Export(FrameworkElement element, string output,
    string name, bool transparent = false)
  {
    element.UpdateLayout();
    var size = element.RenderSize;
    Assert.True(size.Width > 0 && size.Height > 0);
    var drawing = new DrawingVisual();
    using (var context = drawing.RenderOpen())
    {
      if (!transparent)
        context.DrawRoundedRectangle(new SolidColorBrush(
          Color.FromRgb(28, 30, 37)), null, new Rect(size), 32, 32);
      context.DrawRectangle(new VisualBrush(element), null, new Rect(size));
    }
    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * 2),
      (int)Math.Ceiling(size.Height * 2), 192, 192, PixelFormats.Pbgra32);
    bitmap.Render(drawing);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var file = File.Create(Path.Combine(output, name + ".png"));
    encoder.Save(file);
  }
}
