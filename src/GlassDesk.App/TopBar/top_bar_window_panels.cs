using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using GlassDesk.Platform.Shell;

namespace GlassDesk.App.TopBar;

public partial class TopBarWindow
{
  private SystemVolume? volume_;
  private int network_tick_;
  private DateTime calendar_month_, calendar_selected_;

  private void CloseStatusPanels()
  {
    NetworkPopup.IsOpen = false;
    VolumePopup.IsOpen = false;
    CalendarPopup.IsOpen = false;
    ControlCenterPopup.IsOpen = false;
    BatteryPopup.IsOpen = false;
    NotificationsPopup.IsOpen = false;
  }

  private void OnStatusKeyDown(object sender, KeyEventArgs e)
  {
    if (e.Key != Key.Escape) return;
    CloseStatusPanels();
    e.Handled = true;
  }

  private void ToggleStatusPanel(Popup panel)
  {
    var opening = !panel.IsOpen;
    CloseStatusPanels();
    if (!opening) return;
    // 仅弹层交互时激活，点击外部即可收起，顶部栏平时不抢应用焦点。
    GlassDesk.Platform.WindowActivation.TryBringToForeground(handle_);
    panel.IsOpen = true;
  }

  private void RefreshNetworkStatus()
  {
    var kind = NetworkConnectionKind.Disconnected;
    try { kind = NetworkStatus.Read(); }
    catch (NetworkInformationException) { }
    var name = kind == NetworkConnectionKind.Wired ? "WiredIcon"
      : kind == NetworkConnectionKind.Wireless ? "WirelessIcon" : "OfflineIcon";
    NetworkIcon.Data = (Geometry)FindResource(name);
    NetworkIcon.Opacity = kind == NetworkConnectionKind.Disconnected ? .55 : 1;
    var text = kind == NetworkConnectionKind.Wired ? "以太网已连接"
      : kind == NetworkConnectionKind.Wireless ? "Wi-Fi 已连接" : "网络未连接";
    NetworkSummary.Text = NetworkTileStatus.Text = text;
    NetworkButton.ToolTip = text;
  }

  private void OnNetworkAddressChanged(object? sender, EventArgs e)
    => Dispatcher.BeginInvoke(RefreshNetworkStatus);

  private void OnNetwork(object sender, RoutedEventArgs e)
  {
    RefreshNetworkStatus();
    ToggleStatusPanel(NetworkPopup);
  }
  private void OnVolume(object sender, RoutedEventArgs e)
  {
    if (VolumePopup.IsOpen)
    {
      CloseStatusPanels();
      return;
    }
    CloseStatusPanels();
    var volume = SystemVolume.Open();
    var level = 0f;
    var muted = false;
    var available = volume is not null
      && volume.TryRead(out level, out muted);
    // 先初始化控件再保存会话，避免初始 ValueChanged 回写系统音量。
    if (available)
    {
      VolumeSlider.Value = level * 100;
      MuteCheck.IsChecked = muted;
      VolumeReadout.Text = $"{VolumeSlider.Value:0}%";
    }
    else VolumeReadout.Text = "没有可用的播放设备";
    VolumeSlider.IsEnabled = MuteCheck.IsEnabled = available;
    volume_ = volume;
    ToggleStatusPanel(VolumePopup);
  }

  private void OnVolumeChanged(object sender,
    RoutedPropertyChangedEventArgs<double> e)
  {
    if (volume_ is null) return;
    VolumeReadout.Text = volume_.SetLevel((float)(e.NewValue / 100))
      ? $"{e.NewValue:0}%" : "音频设备已不可用";
  }

  private void OnMuteChanged(object sender, RoutedEventArgs e)
  {
    if (volume_ is not null && !volume_.SetMuted(MuteCheck.IsChecked == true))
      VolumeReadout.Text = "音频设备已不可用";
  }

  private void OnVolumePopupClosed(object? sender, EventArgs e)
  {
    volume_?.Dispose();
    volume_ = null;
  }

  private void OnCalendar(object sender, RoutedEventArgs e)
  {
    calendar_selected_ = DateTime.Today;
    calendar_month_ = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    CalendarHeading.Text = DateTime.Today.ToString("M月d日 dddd",
      CultureInfo.CurrentCulture);
    WeekdayHeader.Children.Clear();
    foreach (var day in "日一二三四五六")
      WeekdayHeader.Children.Add(new TextBlock
      {
        Text = day.ToString(), Opacity = .45, FontSize = 11,
        HorizontalAlignment = HorizontalAlignment.Center,
      });
    RenderCalendarMonth();
    ToggleStatusPanel(CalendarPopup);
  }

  private void RenderCalendarMonth()
  {
    CalendarMonthTitle.Text = calendar_month_.ToString("yyyy年M月",
      CultureInfo.CurrentCulture);
    MonthCalendar.Children.Clear();
    var first = calendar_month_.AddDays(-(int)calendar_month_.DayOfWeek);
    for (var index = 0; index < 42; index++)
    {
      var date = first.AddDays(index);
      var button = new Button
      {
        Content = date.Day, Tag = date,
        Style = (Style)FindResource("CalendarDay"),
        Opacity = date.Month == calendar_month_.Month ? 1 : .3,
        Background = date == calendar_selected_
          ? new SolidColorBrush(Color.FromRgb(69, 122, 230))
          : Brushes.Transparent,
      };
      System.Windows.Automation.AutomationProperties.SetName(button,
        date.ToString("D", CultureInfo.CurrentCulture));
      button.Click += (_, _) =>
      {
        calendar_selected_ = date;
        calendar_month_ = new DateTime(date.Year, date.Month, 1);
        RenderCalendarMonth();
      };
      MonthCalendar.Children.Add(button);
    }
  }

  private void OnPreviousMonth(object sender, RoutedEventArgs e)
  {
    calendar_month_ = calendar_month_.AddMonths(-1);
    RenderCalendarMonth();
  }

  private void OnNextMonth(object sender, RoutedEventArgs e)
  {
    calendar_month_ = calendar_month_.AddMonths(1);
    RenderCalendarMonth();
  }

  private void OnShowDesktop(object sender, RoutedEventArgs e)
  {
    CloseStatusPanels();
    DesktopCommands.ShowDesktop();
  }

  private void OnOpenSettings(object sender, RoutedEventArgs e)
  {
    CloseStatusPanels();
    OpenSettings?.Invoke();
  }
  private void OnWifiSettings(object sender, RoutedEventArgs e)
    => OpenStatusSettings("ms-settings:network-wifi");
  private void OnNetworkSettings(object sender, RoutedEventArgs e)
    => OpenStatusSettings("ms-settings:network-status");
  private void OnSoundSettings(object sender, RoutedEventArgs e)
    => OpenStatusSettings("ms-settings:sound");
  private void OnPowerSettings(object sender, RoutedEventArgs e)
    => OpenStatusSettings("ms-settings:powersleep");

  private void OpenStatusSettings(string uri)
  {
    CloseStatusPanels();
    ShellExecutor.Open(uri, null, null);
  }
}
