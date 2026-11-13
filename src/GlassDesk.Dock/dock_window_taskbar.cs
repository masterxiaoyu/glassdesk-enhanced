using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlassDesk.Core.Models.Settings;
using GlassDesk.Platform;
using GlassDesk.Platform.Shell;

namespace GlassDesk.Dock;

public partial class DockWindow
{
  private void OnTrashClick(object sender, RoutedEventArgs e)
    => RunEntryAction("recyclebin");
  private readonly Dictionary<string, BitmapImage?> taskbar_icons_ =
    new(StringComparer.OrdinalIgnoreCase);
  private List<nint> taskbar_order_ = [];

  private void AppendRunningWindows()
  {
    if (_services is null || !Settings.ShowRunningWindows) return;

    // 固定应用沿用已有激活和多窗口选择逻辑，临时项只补齐其余窗口。
    var pinned_handles = _services.Layouts.Dock.PinnedItemIds
      .Select(id => _services.Library.Find(id))
      .Where(entry => entry is { IsUsable: true })
      .SelectMany(entry => _services.Running.FindWindows(entry!))
      .Select(window => window.Handle).ToHashSet();
    var windows = _services.Running.LastWindows
      .Where(window => window.ProcessId != Environment.ProcessId
        && !TaskbarWindowPolicy.IsInputExperience(window.ImagePath)
        && !pinned_handles.Contains(window.Handle))
      .OrderBy(window =>
      {
        var index = taskbar_order_.IndexOf(window.Handle);
        return index < 0 ? int.MaxValue : index;
      }).ToArray();
    // 切换前台会改变枚举 Z 序，保持任务按钮顺序，避免点击时跳位。
    taskbar_order_ = windows.Select(window => window.Handle).ToList();
    var groups = windows.GroupBy(window => window.EffectiveAumid
      ?? window.ImagePath ?? $"process:{window.ProcessId}",
      StringComparer.OrdinalIgnoreCase);
    foreach (var group in groups)
    {
      var window = group.First();
      BitmapImage? icon = null;
      if (window.ImagePath is { } path)
      {
        if (!taskbar_icons_.TryGetValue(path, out icon))
        {
          var cached = _services.Icons.EnsureFor(path);
          icon = cached is null ? null : LoadBitmap(cached.Path);
          taskbar_icons_[path] = icon;
        }
      }
      _models.Add(new DockTileModel
      {
        EntryId = "running:" + group.Key,
        WindowHandle = window.Handle,
        WindowHandles = group.Select(item => item.Handle).ToArray(),
        DisplayName = window.Title,
        ToolTipText = string.Join(Environment.NewLine,
          group.Select(item => item.Title)),
        Icon = icon,
        Glyph = "\uE737",
        DotBrush = DockBuiltinIconBrush,
        IconSize = DockSettings.FixedIconSize * DockScale,
        RunningVisibility = Settings.ShowRunningIndicator
          ? Visibility.Visible : Visibility.Collapsed,
      });
    }
  }

  private void ActivateTaskbarWindow(nint window, bool toggle)
  {
    if (!WindowControl.IsAlive(window))
    {
      _services?.Running.Reconcile();
      return;
    }
    if (toggle && WindowControl.GetForegroundWindow() == window)
      WindowControl.Minimize(window);
    else
    {
      if (!WindowLocator.TryBringToForeground(window))
        ShowFailureNotice("Windows 未允许切换到该窗口。");
    }
  }

  private void ShowTaskbarWindowMenu(DockTileModel tile)
  {
    _autoHide?.PushSuppress(DateTime.UtcNow);
    var foreground_before = WindowControl.GetForegroundWindow();
    var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
    nint menu_handle = 0;
    menu.Opened += (_, _) => menu_handle =
      (PresentationSource.FromVisual(menu) as
        System.Windows.Interop.HwndSource)?.Handle ?? 0;
    menu.Items.Add(MenuItem("切换到此窗口", () =>
      ActivateTaskbarWindow(tile.WindowHandle, false)));
    if (tile.WindowHandles.Count > 1 && _services is not null)
    {
      foreach (var window in _services.Running.LastWindows.Where(window =>
        tile.WindowHandles.Contains(window.Handle)))
        menu.Items.Add(MenuItem(window.Title, () =>
          ActivateTaskbarWindow(window.Handle, false)));
    }
    menu.Items.Add(MenuItem("最小化", () =>
      WindowControl.Minimize(tile.WindowHandle)));
    menu.Items.Add(new Separator());
    // 发送关闭请求，保存确认仍由目标应用处理，不结束进程。
    menu.Items.Add(MenuItem("关闭窗口", () =>
      WindowControl.Close(tile.WindowHandle)));
    menu.Closed += (_, _) =>
    {
      _autoHide?.PopSuppress(DateTime.UtcNow);
      RestorePopupForeground(foreground_before, menu_handle);
    };
    WindowActivation.TryBringToForeground(Handle);
    menu.IsOpen = true;
  }

}
