using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlassDesk.Platform;
using GlassDesk.Platform.Shell;
using GlassDesk.UI.Controls;

namespace GlassDesk.Dock;

public partial class DockWindow
{
  public SystemTrayBridge? SystemTray { get; set; }
  public Action? TrayLayoutChanged { get; set; }
  private Popup? tray_popup_;

  public bool IsSystemTrayOpen => tray_popup_?.IsOpen == true;

  public void CloseSystemTray()
  {
    if (tray_popup_ is not null) tray_popup_.IsOpen = false;
  }

  public void OpenSystemTray(FrameworkElement anchor, bool forDrop = false)
  {
    if (tray_popup_ is { IsOpen: true })
    {
      if (!forDrop) tray_popup_.IsOpen = false;
      return;
    }
    var content = new StackPanel();
    var dragging = false;
    void refresh_content()
    {
      if (dragging || content.Children.OfType<WrapPanel>()
          .SelectMany(panel => panel.Children.OfType<TrayIconButton>())
          .Any(button => button.IsMouseCaptured)) return;
      content.Children.Clear();
      var icons = new WrapPanel
      {
        MaxWidth = 224, MinWidth = 200, MinHeight = 32, AllowDrop = true,
        Background = Brushes.Transparent,
      };
      content.Children.Add(icons);
      icons.DragOver += (_, args) =>
      {
        if (!args.Data.GetDataPresent(TrayIconButton.DragFormat)) return;
        args.Effects = DragDropEffects.Move;
        args.Handled = true;
      };
      icons.Drop += (_, args) =>
      {
        if (_services is null || args.Data.GetData(TrayIconButton.DragFormat)
            is not string id) return;
        args.Handled = true;
        if (!_services.Layouts.Dock.TopBarItemIds.Remove(id)) return;
        try { _services.Layouts.SaveDock(); }
        catch (GlassDesk.Core.Storage.StorageException)
        {
          ShowFailureNotice("收纳位置未能保存，原位置已恢复。");
        }
        TrayLayoutChanged?.Invoke();
        refresh_content();
      };
      // 所有后台图标都可访问，溢出托盘不再受原生折叠区限制。
      foreach (var item in SystemTray?.Items ?? [])
      {
        if (item.Hidden || SystemStatusIcons.IsDedicated(item)
            || _services?.Layouts.Dock.TopBarItemIds
            .Contains(item.PersistentId) == true) continue;
        var button = new TrayIconButton(item);
        button.Click += (_, _) => refresh_content();
        button.RightClicked += (_, _) => tray_popup_!.IsOpen = false;
        button.DragStarted += (_, _) =>
        {
          dragging = true;
          tray_popup_!.StaysOpen = true;
        };
        button.DragFinished += (_, _) =>
        {
          dragging = false;
          tray_popup_!.StaysOpen = false;
          tray_popup_.IsOpen = false;
        };
        icons.Children.Add(button);
      }
      if (icons.Children.Count == 0)
        content.Children.Add(new TextBlock
        {
          Text = SystemTray is { IsRunning: true }
            ? "暂无其他后台图标，可将常驻图标拖回这里。"
            : "后台图标暂时不可用，请切回 Windows 任务栏。",
          TextWrapping = TextWrapping.Wrap, MaxWidth = 280,
          Margin = new Thickness(8),
        });
    }
    refresh_content();
    var panel = new PhoneBorder
    {
      Background = new SolidColorBrush(Color.FromArgb(242, 38, 38, 41)),
      BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)),
      BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
      Padding = new Thickness(12),
      Child = new ScrollViewer
      {
        Content = content, MaxHeight = 360, AllowDrop = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
      },
      Focusable = true,
    };
    tray_popup_ = new Popup
    {
      Child = panel, PlacementTarget = anchor,
      Placement = PlacementMode.Bottom, StaysOpen = forDrop,
      AllowsTransparency = true,
    };
    var popup = tray_popup_;
    var bridge = SystemTray;
    EventHandler update = (_, _) => Dispatcher.BeginInvoke(() =>
    {
      if (popup.IsOpen) refresh_content();
    });
    if (bridge is not null) bridge.Changed += update;
    _autoHide?.PushSuppress(DateTime.UtcNow);
    tray_popup_.Closed += (_, _) =>
    {
      if (bridge is not null) bridge.Changed -= update;
      _autoHide?.PopSuppress(DateTime.UtcNow);
    };
    panel.PreviewKeyDown += (_, args) =>
    {
      if (args.Key == Key.Escape) tray_popup_.IsOpen = false;
    };
    WindowActivation.TryBringToForeground(Handle);
    tray_popup_.IsOpen = true;
    if (!forDrop) panel.Focus();
  }
}
