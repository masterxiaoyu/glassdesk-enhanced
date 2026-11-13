using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GlassDesk.AppServices;
using GlassDesk.Core.Models.Settings;
using GlassDesk.Platform;
using GlassDesk.Platform.Displays;
using GlassDesk.Platform.Shell;

namespace GlassDesk.App.TopBar;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001",
  Justification = "WPF 窗口隐藏和关闭时释放 AppBar 与音频会话。")]
public partial class TopBarWindow : Window
{
  public const double BarHeight = 36;
  private readonly AppServiceHost services_;
  private readonly DispatcherTimer timer_;
  private TopBarReservation? reservation_;
  private nint handle_, application_window_;
  private bool menu_open_;
  private DockSettings settings_ = new();

  public void ApplyPreferences(DockSettings settings)
  {
    settings_ = settings;
    application_window_ = 0;
    if (IsVisible) RefreshApplication();
  }

  public Action? OpenSettings { get; set; }
  public Action<ItemCollection>? PopulateApplicationMenu { get; set; }
  public Action? ExitApplication { get; set; }
  public Action<FrameworkElement, bool>? OpenTray { get; set; }
  public Action? CloseTray { get; set; }
  public Func<bool>? IsTrayOpen { get; set; }
  public bool IsWorkAreaReserved => reservation_?.IsReserved == true;
  public bool NativeTaskbarHidden { get; set; }

  public TopBarWindow(AppServiceHost services)
  {
    services_ = services;
    InitializeComponent();
    timer_ = new DispatcherTimer
    {
      Interval = TimeSpan.FromMilliseconds(750),
    };
    timer_.Tick += (_, _) =>
    {
      RefreshApplication();
      if (++network_tick_ % 8 == 0)
      {
        RefreshNetworkStatus();
        RefreshBatteryStatus();
        if (NotificationsPopup.IsOpen) _ = RefreshNotificationsAsync();
      }
    };
  }

  public void SetEnabled(bool enabled)
  {
    if (enabled)
    {
      if (!IsVisible)
        System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged
          += OnNetworkAddressChanged;
      Show();
      RefreshPlacement();
      RefreshApplication();
      RefreshNetworkStatus();
      RefreshBatteryStatus();
      RefreshTrayItems();
      timer_.Start();
    }
    else
    {
      System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged
        -= OnNetworkAddressChanged;
      timer_.Stop();
      CloseStatusPanels();
      Hide();
      reservation_?.Dispose();
    }
  }

  public void RefreshPlacement()
  {
    if (!IsVisible || reservation_ is null) return;
    var display = DisplayService.Primary();
    if (display is null) return;
    var rect = reservation_.Reserve(display, display.ToPixels(BarHeight),
      NativeTaskbarHidden);
    DisplayService.MoveWindowToPixelRect(handle_, rect);
  }

  private void RefreshApplication()
  {
    Clock.Text = DateTime.Now.ToString("M月d日 ddd HH:mm",
      CultureInfo.CurrentCulture).Replace("星期", "周", StringComparison.Ordinal);
    var foreground = WindowControl.GetForegroundWindow();
    var identity = services_.Running.LastWindows.FirstOrDefault(window =>
      window.Handle == foreground);
    // 只在本栏菜单交互期间保留目标，切到桌面或设置时不能残留旧名称。
    if (menu_open_ || NetworkPopup.IsOpen || VolumePopup.IsOpen
        || CalendarPopup.IsOpen || ControlCenterPopup.IsOpen
        || BatteryPopup.IsOpen || NotificationsPopup.IsOpen
        || tray_dragging_ || IsTrayOpen?.Invoke() == true
        || foreground == handle_) return;
    if (identity is not null
        && TaskbarWindowPolicy.IsInputExperience(identity.ImagePath))
      return;
    if (identity is null)
    {
      application_window_ = 0;
      var own_window = Application.Current?.Windows.Cast<Window>().Any(window =>
        new WindowInteropHelper(window).Handle == foreground) == true;
      ApplicationName.Text = own_window ? "GlassDesk" : "桌面";
      ApplicationName.ToolTip = own_window ? "GlassDesk" : "桌面或系统界面";
      ApplicationMenus.Children.Clear();
      return;
    }
    if (foreground == application_window_) return;
    application_window_ = foreground;
    ApplicationName.Text = identity.ProcessId == Environment.ProcessId
      ? "GlassDesk" : identity.ImagePath is { } path
        ? Path.GetFileNameWithoutExtension(path) : identity.Title;
    if (identity.ProcessId != Environment.ProcessId
        && identity.ImagePath is { } image)
    {
      // 文件名可能沿用旧品牌（如 Codex 的 ChatGPT.exe），优先用产品名称。
      try
      {
        var version = FileVersionInfo.GetVersionInfo(image);
        var name = string.IsNullOrWhiteSpace(version.FileDescription)
          ? version.ProductName : version.FileDescription;
        if (!string.IsNullOrWhiteSpace(name)) ApplicationName.Text = name;
      }
      catch (Exception error) when (error is IOException
          or UnauthorizedAccessException or Win32Exception)
      {
        // 应用退出或映像不可读时仍可显示已有进程名。
      }
    }
    ApplicationName.ToolTip = "当前前台应用：" + identity.Title;
    ApplicationMenus.Children.Clear();
    var menu = NativeApplicationMenu.ForWindow(foreground);
    var entries = settings_.TopBarShowApplicationMenus
      ? NativeApplicationMenu.Read(menu) : [];
    for (var index = 0; index < entries.Count; index++)
    {
      var entry = entries[index];
      if (entry.Separator || entry.Submenu == 0) continue;
      var position = index;
      var target = foreground;
      var button = new Button
      {
        Content = entry.Text.Replace("&", "", StringComparison.Ordinal),
        IsEnabled = entry.Enabled,
      };
      button.Click += (_, _) =>
      {
        NativeApplicationMenu.Initialize(target, entry.Submenu, position);
        var popup = new ContextMenu();
        PopulateMenu(popup.Items, target, entry.Submenu);
        ShowMenu(button, popup);
      };
      ApplicationMenus.Children.Add(button);
    }
  }

  private static void PopulateMenu(ItemCollection items, nint target,
    nint menu)
  {
    items.Clear();
    var entries = NativeApplicationMenu.Read(menu);
    for (var index = 0; index < entries.Count; index++)
    {
      var entry = entries[index];
      if (entry.Separator)
      {
        items.Add(new Separator());
        continue;
      }
      var item = new MenuItem
      {
        Header = entry.Text.Replace("_", "__", StringComparison.Ordinal)
          .Replace("&", "_", StringComparison.Ordinal),
        IsEnabled = entry.Enabled, IsChecked = entry.Checked,
      };
      if (entry.Submenu != 0)
      {
        var position = index;
        item.Items.Add(new MenuItem());
        item.SubmenuOpened += (_, args) =>
        {
          if (!ReferenceEquals(args.OriginalSource, item)) return;
          NativeApplicationMenu.Initialize(target, entry.Submenu, position);
          PopulateMenu(item.Items, target, entry.Submenu);
        };
      }
      else item.Click += (_, _) =>
        NativeApplicationMenu.Execute(target, entry.Command);
      items.Add(item);
    }
  }

  private void ShowMenu(FrameworkElement anchor, ContextMenu menu)
  {
    menu.PlacementTarget = anchor;
    menu.Placement = PlacementMode.Bottom;
    menu_open_ = true;
    menu.Closed += (_, _) => menu_open_ = false;
    WindowActivation.TryBringToForeground(handle_);
    menu.IsOpen = true;
  }

  private void OnStartContextMenu(object sender, MouseButtonEventArgs e)
  {
    // 开始图标保留系统快捷菜单，阻止事件继续打开 GlassDesk 右键菜单。
    e.Handled = true;
    CloseStatusPanels();
    WindowsShellCommands.OpenPowerUserMenu();
  }

  private void OnGlassDeskContextMenu(object sender, MouseButtonEventArgs e)
  {
    // 产品菜单只属于 GlassDesk 名称，空白区和其他应用不显示此菜单。
    if (e.Handled || ApplicationName.Text != "GlassDesk") return;
    CloseStatusPanels();
    var menu = new ContextMenu();
    PopulateApplicationMenu?.Invoke(menu.Items);
    if (menu.Items.Count == 0) return;
    ShowMenu((FrameworkElement)sender, menu);
    e.Handled = true;
  }

  private void OnBarMouseDown(object sender, MouseButtonEventArgs e)
  {
    if (e.ClickCount != 2
        || !settings_.TopBarDoubleClickDesktop) return;
    // 双击按钮仍执行按钮本身的操作，仅空白处响应显示桌面。
    for (var node = e.OriginalSource as Visual;
         node is not null; node = VisualTreeHelper.GetParent(node) as Visual)
      if (node is ButtonBase) return;
    DesktopCommands.ShowDesktop();
    e.Handled = true;
  }

  private void OnVolumeWheel(object sender, MouseWheelEventArgs e)
  {
    if (!settings_.TopBarScrollVolume) return;
    using var volume = SystemVolume.Open();
    if (volume is null || !volume.TryRead(out var level, out _)) return;
    var next = Math.Clamp(level + e.Delta / 120f * .02f, 0, 1);
    if (!volume.SetLevel(next)) return;
    if (VolumePopup.IsOpen) VolumeSlider.Value = next * 100;
    VolumeButton.ToolTip = $"音量 {next:P0}";
    e.Handled = true;
  }

  private void OnApplicationMenu(object sender, RoutedEventArgs e)
  {
    CloseStatusPanels();
    var target = application_window_;
    var menu = new ContextMenu();
    var hide = new MenuItem
    {
      Header = "隐藏 " + ApplicationName.Text, IsEnabled = target != 0,
    };
    hide.Click += (_, _) => WindowControl.Minimize(target);
    menu.Items.Add(hide);
    var close = new MenuItem { Header = "关闭当前窗口", IsEnabled = target != 0 };
    close.Click += (_, _) => WindowControl.Close(target);
    menu.Items.Add(close);
    ShowMenu((FrameworkElement)sender, menu);
  }

  private void OnStart(object sender, RoutedEventArgs e)
  {
    CloseStatusPanels();
    WindowsShellCommands.OpenStart();
  }

  private void OnTray(object sender, RoutedEventArgs e)
  {
    CloseStatusPanels();
    OpenTray?.Invoke((FrameworkElement)sender, false);
  }
  private void OnQuickSettings(object sender, RoutedEventArgs e)
  {
    RefreshNetworkStatus();
    ToggleStatusPanel(ControlCenterPopup);
  }
  private async void OnNotifications(object sender, RoutedEventArgs e)
  {
    ToggleStatusPanel(NotificationsPopup);
    if (NotificationsPopup.IsOpen) await RefreshNotificationsAsync();
  }

  protected override void OnSourceInitialized(EventArgs e)
  {
    base.OnSourceInitialized(e);
    handle_ = new WindowInteropHelper(this).Handle;
    WindowStyles.MakeNoActivate(handle_);
    WindowAcrylic.TryApply(handle_, 180, 30, 30, 33);
    reservation_ = new TopBarReservation(handle_);
    HwndSource.FromHwnd(handle_)?.AddHook(OnMessage);
  }

  private nint OnMessage(nint window, int message, nint wparam,
    nint lparam, ref bool handled)
  {
    if (message == TopBarReservation.CallbackMessage && wparam == 1)
      Dispatcher.BeginInvoke(RefreshPlacement, DispatcherPriority.Background);
    // 插拔电源时立即更新图标，定时刷新只作为状态校准。
    if (message == 0x0218 && wparam == 0x000A) RefreshBatteryStatus();
    return 0;
  }

  protected override void OnClosed(EventArgs e)
  {
    System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged
      -= OnNetworkAddressChanged;
    timer_.Stop();
    SystemTray = null;
    CloseStatusPanels();
    reservation_?.Dispose();
    base.OnClosed(e);
  }
}
