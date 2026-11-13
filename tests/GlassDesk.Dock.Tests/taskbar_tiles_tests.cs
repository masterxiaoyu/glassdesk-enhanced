using GlassDesk.Dock;
using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using GlassDesk.Core.Storage;
using GlassDesk.Settings;
using GlassDesk.UI.Settings;

namespace GlassDesk.Dock.Tests;

public class TaskbarTilesTests
{
  [Fact]
  public void 任务栏设置在修改其他Dock选项后仍保存并可重新加载()
  {
    StaTest.Run(() =>
    {
      var root = Path.Combine(Path.GetTempPath(),
        "GlassDesk-taskbar-settings-" + Guid.NewGuid().ToString("N"));
      var window = new SettingsWindow();
      try
      {
        var data = new GlassDeskDataStore(new AppPaths(root));
        data.LoadAll();
        DockSettingsHost.Initialize(Dispatcher.CurrentDispatcher);
        window.AttachDataStore(data, null);
        ((CheckBox)window.FindName("ShowRunningWindowsCheck")).IsChecked = false;
        ((ComboBox)window.FindName("TaskbarModeCombo")).SelectedIndex = 1;
        Assert.Null(window.FindName("HideNativeTaskbarCheck"));
        ((Slider)window.FindName("DockOffsetXSlider")).Value = 19;
        var reopened = new GlassDeskDataStore(new AppPaths(root));
        reopened.LoadAll();
        Assert.False(reopened.Settings.Current.Dock.ShowRunningWindows);
        Assert.True(reopened.Settings.Current.Dock.ShowTopBar);
        Assert.True(reopened.Settings.Current.Dock.HideNativeTaskbar);
        Assert.Equal(19, reopened.Settings.Current.Dock.OffsetX);
        ((ComboBox)window.FindName("TaskbarModeCombo")).SelectedIndex = 0;
        reopened.LoadAll();
        Assert.False(reopened.Settings.Current.Dock.ShowTopBar);
        Assert.False(reopened.Settings.Current.Dock.HideNativeTaskbar);
      }
      finally
      {
        window.Close();
        if (Directory.Exists(root)) Directory.Delete(root, true);
      }
    });
  }

  [Fact]
  public void 临时窗口不进入固定应用排序列表()
  {
    StaTest.Run(() =>
    {
      var strip = new DockTileStrip();
      strip.SetTiles([
        new DockTileModel { EntryId = "pinned-app" },
        new DockTileModel { EntryId = "window:123", WindowHandle = 123 },
        new DockTileModel { EntryId = "window:456", WindowHandle = 456 },
      ]);
      Assert.Equal(["pinned-app"], strip.ReorderCandidateIds);
      strip.SetTiles([
        new DockTileModel { EntryId = "window:456", WindowHandle = 456 },
      ]);
      Assert.Empty(strip.ReorderCandidateIds);
    });
  }
}
