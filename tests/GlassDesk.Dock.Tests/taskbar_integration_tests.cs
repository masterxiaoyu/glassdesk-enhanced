using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Threading;
using GlassDesk.App.TopBar;
using GlassDesk.AppServices;
using GlassDesk.Core.Models;
using GlassDesk.Core.Models.Settings;
using GlassDesk.Core.Storage;
using GlassDesk.Platform;
using GlassDesk.Platform.Shell;
using GlassDesk.UI.Settings;

namespace GlassDesk.Dock.Tests;

public class TaskbarIntegrationTests
{
  [Fact]
  public void 外部运行窗口可切换最小化更新标题且固定后不重复显示()
  {
    StaTest.Run(() =>
    {
      var root = Path.Combine(Path.GetTempPath(),
        "GlassDesk-taskbar-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(root);
      var executable = CompatibilityProbePath.File("GlassCaptureProbe.exe");
      var start = new ProcessStartInfo(executable)
      {
        UseShellExecute = false, CreateNoWindow = true,
      };
      start.ArgumentList.Add("--taskbar-fixture");
      start.ArgumentList.Add(root);
      using var process = Process.Start(start)!;
      var window = new DockWindow();
      try
      {
        PumpUntil(() => File.Exists(Path.Combine(root, "ready.txt")));
        var handle = (nint)long.Parse(File.ReadAllText(
          Path.Combine(root, "ready.txt")), CultureInfo.InvariantCulture);
        var data = new GlassDeskDataStore(new AppPaths(
          Path.Combine(root, "Data")));
        data.LoadAll();
        using var services = new AppServiceHost(data);
        var settings = new DockSettingsHost(Dispatcher.CurrentDispatcher);
        window.AttachServices(services, settings, () => { });
        services.Running.Start();
        window.Show();
        PumpUntil(() => Tiles(window).Any(tile => tile.WindowHandle == handle));
        Assert.Empty(data.Layouts.Dock.PinnedItemIds);

        var activate = typeof(DockWindow).GetMethod("ActivateTaskbarWindow",
          BindingFlags.Instance | BindingFlags.NonPublic)!;
        activate.Invoke(window, [handle, false]);
        PumpUntil(() => WindowControl.GetForegroundWindow() == handle,
          () => "测试初始窗口未取得前台。");
        var bar = new TopBarWindow(services);
        try
        {
          typeof(TopBarWindow).GetMethod("RefreshApplication",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(bar, null);
          Assert.Equal("GlassDesk 测试应用",
            ((TextBlock)bar.FindName("ApplicationName")).Text);
        }
        finally { bar.Close(); }
        activate.Invoke(window, [handle, true]);
        PumpUntil(() => WindowControl.IsMinimized(handle),
          () => "应用未能最小化。");
        activate.Invoke(window, [handle, false]);
        PumpUntil(() => !WindowControl.IsMinimized(handle));

        File.WriteAllText(Path.Combine(root, "command.txt"), "title");
        PumpUntil(() => Tiles(window).Any(tile => tile.WindowHandle == handle
          && tile.ToolTipText == "GlassDesk 已更新标题"));

        // 同一个外部进程固定后，窗口由固定项承接，不再出现重复临时项。
        var entry = new AppEntry
        {
          Id = "fixture", DisplayName = "测试应用", Target = executable,
        };
        data.Library.Add(entry);
        data.Layouts.Dock.PinnedItemIds.Add(entry.Id);
        window.RefreshTiles();
        Assert.DoesNotContain(Tiles(window), tile =>
          tile.WindowHandle == handle);
        Assert.Contains(Tiles(window), tile => tile.EntryId == entry.Id);

        using var bridge = new SystemTrayBridge();
        Assert.True(bridge.Start());
        PumpUntil(() => bridge.Items.Any(item =>
          item.Owner == handle && item.Id == 173));
        var icon = Assert.Single(bridge.Items, item =>
          item.Owner == handle && item.Id == 173);
        Assert.True(SystemTrayBridge.SendCallback(icon, 0x0203, 100, 200));
        PumpUntil(() => File.Exists(Path.Combine(root, "callback.txt")));
        Assert.Equal("515", File.ReadAllText(Path.Combine(root, "callback.txt")));
      }
      finally
      {
        window.Close();
        File.WriteAllText(Path.Combine(root, "command.txt"), "exit");
        if (!process.WaitForExit(3000)) process.Kill();
        Directory.Delete(root, true);
      }
    });
  }

  private static List<DockTileModel> Tiles(DockWindow window)
    => (List<DockTileModel>)typeof(DockWindow).GetField("_models",
      BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;

  private static void PumpUntil(Func<bool> condition,
    Func<string>? diagnostic = null,
    [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))]
    string? expression = null)
  {
    var clock = Stopwatch.StartNew();
    while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(5))
    {
      Dispatcher.CurrentDispatcher.Invoke(() => { },
        DispatcherPriority.ApplicationIdle);
      Thread.Sleep(20);
    }
    Assert.True(condition(), diagnostic?.Invoke()
      ?? "等待任务栏状态超时：" + expression);
  }
}
