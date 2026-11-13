using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GlassDesk.Platform.Shell;

namespace GlassDesk.App.TopBar;

public partial class TopBarWindow
{
  private bool notification_loading_;
  private NotificationAccess notification_access_;

  private void RefreshBatteryStatus()
  {
    var battery = SystemBattery.Read();
    BatteryButton.Visibility = battery?.HasBattery == true
      ? Visibility.Visible : Visibility.Collapsed;
    BatteryFill.Width = (battery?.Percent ?? 0) * .1;
    // 依据供电来源显示闪电，充满后接着电源也能与电池供电区分。
    BatteryButton.Tag = battery?.Connected == true;
    BatteryPercent.Text = battery?.Percent is { } percent ? $"{percent}%" : "—";
    var state = battery is null ? "无法读取电池状态"
      : !battery.HasBattery ? "未检测到电池"
      : battery.Charging ? "正在充电"
      : battery.Connected == true ? "已接通电源" : "正在使用电池";
    if (battery?.EnergySaver == true) state += " · 节能模式";
    if (battery is { Connected: false, RemainingSeconds: { } seconds })
      state += $"\n预计剩余 {seconds / 3600} 小时 {seconds % 3600 / 60} 分钟";
    BatterySummary.Text = state;
    BatteryButton.ToolTip = $"电池 {BatteryPercent.Text} · {state}";
  }

  private void OnBattery(object sender, RoutedEventArgs e)
  {
    RefreshBatteryStatus();
    ToggleStatusPanel(BatteryPopup);
  }

  private async void OnRefreshNotifications(object sender, RoutedEventArgs e)
    => await RefreshNotificationsAsync();

  private async Task RefreshNotificationsAsync(bool requestAccess = false)
  {
    if (notification_loading_) return;
    notification_loading_ = true;
    NotificationStatus.Text = "正在同步通知…";
    try
    {
      var snapshot = await SystemNotifications.ReadAsync(requestAccess);
      notification_access_ = snapshot.Access;
      var component_available = File.Exists(Path.Combine(
        AppContext.BaseDirectory, "GlassDesk.Identity.msix"));
      NotificationStatus.Text = snapshot.Access ==
          NotificationAccess.RequiresInstallation && !component_available
        ? "系统通知同步暂未开放。" : snapshot.Status;
      NotificationAccessButton.Visibility = snapshot.Access is
          NotificationAccess.RequiresInstallation
          or NotificationAccess.RequiresPermission
        && (snapshot.Access != NotificationAccess.RequiresInstallation
          || component_available)
        ? Visibility.Visible : Visibility.Collapsed;
      NotificationAccessButton.Content = snapshot.Access ==
          NotificationAccess.RequiresInstallation
        ? "安装通知组件…" : "授权通知访问…";
      NotificationItems.Children.Clear();
      foreach (var item in snapshot.Items)
      {
        var content = new StackPanel();
        var header = new DockPanel();
        var dismiss = new Button
        {
          Content = "×", Style = (Style)FindResource("PanelAction"),
          ToolTip = "清除此通知", Padding = new Thickness(6),
        };
        DockPanel.SetDock(dismiss, System.Windows.Controls.Dock.Right);
        header.Children.Add(dismiss);
        header.Children.Add(new TextBlock
        {
          Text = item.Application + " · " + item.Created.ToLocalTime()
            .ToString("HH:mm", CultureInfo.CurrentCulture),
          FontSize = 11, Opacity = .6,
          VerticalAlignment = VerticalAlignment.Center,
          TextTrimming = TextTrimming.CharacterEllipsis,
        });
        content.Children.Add(header);
        content.Children.Add(new TextBlock
        {
          Text = item.Title, FontWeight = FontWeights.SemiBold,
          TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 4),
        });
        content.Children.Add(new TextBlock
        {
          Text = item.Body, TextWrapping = TextWrapping.Wrap, Opacity = .85,
        });
        var card = new Border
        {
          Child = content, CornerRadius = new CornerRadius(10),
          Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
          Padding = new Thickness(12), Margin = new Thickness(0, 4, 0, 4),
        };
        dismiss.Click += async (_, _) =>
        {
          try
          {
            SystemNotifications.Dismiss(item.Id);
            await RefreshNotificationsAsync();
          }
          catch (Exception error) when (error is COMException
            or UnauthorizedAccessException)
          {
            NotificationStatus.Text = "无法清除此通知，请检查通知访问权限。";
          }
        };
        NotificationItems.Children.Add(card);
      }
    }
    finally { notification_loading_ = false; }
  }

  private async void OnNotificationAccess(object sender, RoutedEventArgs e)
  {
    if (notification_access_ != NotificationAccess.RequiresInstallation)
    {
      await RefreshNotificationsAsync(requestAccess: true);
      return;
    }
    NotificationAccessButton.IsEnabled = false;
    try
    {
      NotificationStatus.Text = "正在安装通知组件…";
      var result = await SystemNotifications.InstallAsync(
        Path.Combine(AppContext.BaseDirectory, "GlassDesk.Identity.msix"),
        AppContext.BaseDirectory);
      NotificationStatus.Text = result.Message;
    }
    catch (Exception error) when (error is COMException
      or UnauthorizedAccessException or OperationCanceledException
      or IOException)
    {
      NotificationStatus.Text = "通知组件安装失败：" + error.Message;
    }
    finally { NotificationAccessButton.IsEnabled = true; }
  }
}
