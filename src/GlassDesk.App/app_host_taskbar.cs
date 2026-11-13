using System.Windows.Threading;
using GlassDesk.Platform.Shell;
using GlassDesk.App.Tray;
using GlassDesk.App.TopBar;

namespace GlassDesk.App;

internal sealed partial class AppHost
{
  private readonly NativeTaskbarSession native_taskbar_ = new();
  private readonly SystemTrayBridge system_tray_ = new();
  private DispatcherTimer? native_taskbar_timer_;
  private TopBarWindow? top_bar_;

  private void InitializeTaskbarIntegration()
  {
    _dockSettings!.Changed += OnTaskbarSettingsChanged;
    native_taskbar_timer_ = new DispatcherTimer
    {
      Interval = TimeSpan.FromSeconds(1),
    };
    native_taskbar_timer_.Tick += (_, _) =>
    {
      system_tray_.Refresh();
      native_taskbar_.Refresh();
    };
    _dock!.SystemTray = system_tray_;
    _dock.TrayLayoutChanged = () => top_bar_?.RefreshTrayItems();
    ApplyNativeTaskbar();
  }

  private void OnTaskbarSettingsChanged(object? sender, EventArgs e)
  {
    ApplyNativeTaskbar();
    ReconcilePerformance();
  }

  private void ApplyNativeTaskbar()
  {
    if (_dockSettings is null) return;
    var use_top_bar = _dockSettings.Settings.ShowTopBar;
    var changing_native = native_taskbar_.IsHidden
      != use_top_bar;
    if (changing_native) top_bar_?.SetEnabled(false);
    if (_dockSettings.Settings.ShowTopBar)
    {
      var starting = !system_tray_.IsRunning;
      if (!system_tray_.Start())
        AppendToLog("托盘消息桥启动失败，原生托盘保持可用。");
      else if (starting && _tray is not null)
        // 本程序使用仅消息窗口，不接收广播，需要主动重新提交自己的图标。
        _tray.Show(TrayIconArtwork.CreateHandle(32), "GlassDesk");
    }
    else system_tray_.Dispose();
    if (_dockSettings.Settings.ShowTopBar && top_bar_ is null)
    {
      top_bar_ = new TopBarWindow(_services!)
      {
        OpenSettings = ShowSettings, ExitApplication = ExitApplication,
        PopulateApplicationMenu = AppendApplicationMenu,
        OpenTray = (anchor, forDrop) => _dock!.OpenSystemTray(anchor, forDrop),
        CloseTray = () => _dock!.CloseSystemTray(),
        IsTrayOpen = () => _dock!.IsSystemTrayOpen,
        SystemTray = system_tray_,
      };
      top_bar_.Closed += (_, _) => top_bar_ = null;
    }
    if (!native_taskbar_.SetHidden(use_top_bar,
        out var error))
    {
      _dockSettings.Settings.HideNativeTaskbar = false;
      _dockSettings.Settings.ShowTopBar = false;
      system_tray_.Dispose();
      System.Windows.MessageBox.Show(error, "任务栏隐藏失败");
    }
    if (top_bar_ is not null)
    {
      top_bar_.NativeTaskbarHidden = native_taskbar_.IsHidden;
      top_bar_.ApplyPreferences(_dockSettings.Settings);
      top_bar_.SetEnabled(_dockSettings.Settings.ShowTopBar);
    }
    // 顶部空间调整可能触发原生栏重显，收尾时重新隐藏，但不重写工作区。
    native_taskbar_.Refresh();
    system_tray_.Refresh();
    native_taskbar_timer_!.IsEnabled = native_taskbar_.IsHidden
      || system_tray_.IsRunning;
    // 一个模式对应一根任务栏：先释放旧栏，再启用新栏。
    QueueDisplayRefresh();
  }

  private void DisposeTaskbarIntegration()
  {
    native_taskbar_timer_?.Stop();
    if (_dockSettings is not null)
      _dockSettings.Changed -= OnTaskbarSettingsChanged;
    // 正常退出和宿主清理都归还任务栏，不能把恢复只放在窗口事件中。
    system_tray_.Dispose();
    top_bar_?.Close();
    native_taskbar_.Dispose();
  }

  private void RestoreNativeTaskbar()
  {
    if (_data is null) return;
    _data.Settings.Current.Dock.HideNativeTaskbar = false;
    _data.Settings.Current.Dock.ShowTopBar = false;
    // 恢复先于文件写入，即使磁盘不可写也能取回系统任务栏。
    top_bar_?.SetEnabled(false);
    native_taskbar_.SetHidden(false, out _);
    QueueDisplayRefresh();
    _dockSettings?.Refresh();
    try { _data.Settings.Save(); }
    catch (System.IO.IOException error)
    {
      System.Windows.MessageBox.Show("任务栏已恢复，保存设置失败：" +
        error.Message, "GlassDesk");
    }
  }
}
