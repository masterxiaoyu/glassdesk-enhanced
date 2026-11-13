using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GlassDesk.Core.Storage;
using GlassDesk.Platform.Shell;
using GlassDesk.UI.Controls;

namespace GlassDesk.App.TopBar;

public partial class TopBarWindow
{
  private SystemTrayBridge? system_tray_;
  private bool tray_dragging_;

  public SystemTrayBridge? SystemTray
  {
    get => system_tray_;
    set
    {
      if (system_tray_ is not null) system_tray_.Changed -= OnTrayChanged;
      system_tray_ = value;
      if (value is not null) value.Changed += OnTrayChanged;
      RefreshTrayItems();
    }
  }

  private System.Windows.Threading.DispatcherOperation? tray_refresh_;

  private void OnTrayChanged(object? sender, EventArgs e)
  {
    // 全屏休眠期间不重建隐藏图标；一轮外壳消息只提交一次界面刷新。
    if (!IsVisible || tray_refresh_?.Status
        == System.Windows.Threading.DispatcherOperationStatus.Pending) return;
    tray_refresh_ = Dispatcher.BeginInvoke(RefreshTrayItems);
  }

  public void RefreshTrayItems()
  {
    if (tray_dragging_ || PinnedTrayHost.Children.OfType<TrayIconButton>()
        .Any(button => button.IsMouseCaptured)) return;
    var visible = new List<SystemTrayItem>();
    foreach (var id in services_.Layouts.Dock.TopBarItemIds)
    {
      var item = system_tray_?.Items.FirstOrDefault(entry =>
        entry.PersistentId == id && !entry.Hidden
          && !SystemStatusIcons.IsDedicated(entry));
      if (item is null) continue;
      visible.Add(item);
    }
    // 其他后台应用的状态更新不应反复分配常驻图标的 WPF 位图。
    if (PinnedTrayHost.Children.OfType<TrayIconButton>()
        .Select(button => button.Item).SequenceEqual(visible)) return;
    PinnedTrayHost.Children.Clear();
    foreach (var item in visible)
    {
      var button = new TrayIconButton(item, BarHeight)
      {
        Tag = item.PersistentId,
      };
      button.DragStarted += (_, _) => tray_dragging_ = true;
      button.Click += (_, _) => RefreshTrayItems();
      button.DragFinished += (_, _) =>
      {
        tray_dragging_ = false;
        CloseTray?.Invoke();
        RefreshTrayItems();
      };
      PinnedTrayHost.Children.Add(button);
    }
  }

  private void OnTrayDragOver(object sender, DragEventArgs e)
  {
    if (!e.Data.GetDataPresent(TrayIconButton.DragFormat)) return;
    e.Effects = DragDropEffects.Move;
    e.Handled = true;
    PinnedTrayHost.Background = new SolidColorBrush(
      Color.FromArgb(42, 255, 255, 255));
  }

  private void OnTrayDragLeave(object sender, DragEventArgs e)
    => PinnedTrayHost.Background = Brushes.Transparent;

  private void OnTrayDrop(object sender, DragEventArgs e)
  {
    if (e.Data.GetData(TrayIconButton.DragFormat) is not string id
        || system_tray_?.Items.Any(item => item.PersistentId == id
          && !SystemStatusIcons.IsDedicated(item)) != true)
      return;
    e.Handled = true;
    PinnedTrayHost.Background = Brushes.Transparent;
    var x = e.GetPosition(PinnedTrayHost).X;
    var target = PinnedTrayHost.Children.OfType<FrameworkElement>()
      .FirstOrDefault(child => x < child.TranslatePoint(new Point(),
        PinnedTrayHost).X + child.ActualWidth / 2)?.Tag as string;
    PinTrayItem(id, target);
  }

  private void PinTrayItem(string id, string? target)
  {
    if (id == target) return;
    var order = services_.Layouts.Dock.TopBarItemIds;
    order.Remove(id);
    var index = target is null ? -1 : order.IndexOf(target);
    order.Insert(index < 0 ? order.Count : index, id);
    try { services_.Layouts.SaveDock(); }
    catch (StorageException)
    {
      Dispatcher.BeginInvoke(() => MessageBox.Show(
        "菜单栏图标未能保存，原来的位置已恢复。", "GlassDesk"));
    }
    RefreshTrayItems();
  }

  private void OnOverflowDragEnter(object sender, DragEventArgs e)
  {
    if (e.Data.GetDataPresent(TrayIconButton.DragFormat))
      OpenTray?.Invoke(OverflowButton, true);
  }
}
