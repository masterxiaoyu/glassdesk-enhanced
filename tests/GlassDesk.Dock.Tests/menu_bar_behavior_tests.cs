using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using GlassDesk.App.TopBar;
using GlassDesk.AppServices;
using GlassDesk.Core.Storage;
using GlassDesk.Platform.Shell;
using GlassDesk.UI.Controls;
using GlassDesk.UI.Settings;

namespace GlassDesk.Dock.Tests;

public sealed class MenuBarBehaviorTests
{
  [Theory]
  [InlineData("7820ae76-23e3-4229-82c1-e41cb67d5b9c", true)]
  [InlineData("7820ae73-23e3-4229-82c1-e41cb67d5b9c", true)]
  [InlineData("7820ae75-23e3-4229-82c1-e41cb67d5b9c", true)]
  [InlineData("7820ae78-23e3-4229-82c1-e41cb67d5b9c", false)]
  [InlineData("05438b20-0a9a-4c7d-bb4e-a26f4bca7eab", true)]
  [InlineData("025a5937-a6be-4686-a844-36fe4bec8b6d", true)]
  [InlineData("18e415f0-5670-4c0b-8af2-79c7e2124a30", false)]
  public void 仅系统状态图标使用独立入口(string identity, bool dedicated)
  {
    var item = new SystemTrayItem(0, 1, Guid.Parse(identity), 0, 4,
      "音量与电池工具", false, null);
    Assert.Equal(dedicated, SystemStatusIcons.IsDedicated(item));
  }

  [Theory]
  [InlineData(false, false, NetworkConnectionKind.Disconnected)]
  [InlineData(false, true, NetworkConnectionKind.Wireless)]
  [InlineData(true, false, NetworkConnectionKind.Wired)]
  [InlineData(true, true, NetworkConnectionKind.Wired)]
  public void 网络有线优先且不依赖枚举顺序(bool wired, bool wireless,
    NetworkConnectionKind expected)
  {
    var types = new List<NetworkInterfaceType>();
    if (wireless) types.Add(NetworkInterfaceType.Wireless80211);
    if (wired) types.Add(NetworkInterfaceType.Ethernet);
    Assert.Equal(expected, NetworkStatus.Select(types));
    types.Reverse();
    Assert.Equal(expected, NetworkStatus.Select(types));
  }

  [Theory]
  [InlineData("TextInputHost.exe", true, true)]
  [InlineData("InputApp.exe", true, true)]
  [InlineData("notepad.exe", true, false)]
  [InlineData("TextInputHost.exe", false, false)]
  [InlineData(null, false, false)]
  public void 仅过滤系统输入宿主(string? file, bool system, bool excluded)
  {
    var path = file is null ? null : Path.Combine(system
      ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "SystemApps", "Example") : Path.GetTempPath(), file);
    Assert.Equal(excluded, TaskbarWindowPolicy.IsInputExperience(path));
  }

  [Fact]
  public void 托盘拖入常驻排序并拖回收纳后布局正确()
  {
    StaTest.Run(() =>
    {
      var root = Path.Combine(Path.GetTempPath(),
        "GlassDesk-menu-tray-" + Guid.NewGuid().ToString("N"));
      var data = new GlassDeskDataStore(new AppPaths(root));
      data.LoadAll();
      using var services = new AppServiceHost(data);
      using var bridge = new SystemTrayBridge();
      var items = (Dictionary<(nint, uint), SystemTrayItem>)typeof(
        SystemTrayBridge).GetField("items_", BindingFlags.Instance
          | BindingFlags.NonPublic)!.GetValue(bridge)!;
      var first = new SystemTrayItem(0, 1, Guid.NewGuid(), 0, 4,
        "工具一", false, null);
      var second = new SystemTrayItem(0, 2, Guid.NewGuid(), 0, 4,
        "工具二", false, null);
      items[(0, 1)] = first;
      items[(0, 2)] = second;
      var bar = new TopBarWindow(services) { SystemTray = bridge };
      var dock = new DockWindow { SystemTray = bridge };
      dock.TrayLayoutChanged = bar.RefreshTrayItems;
      try
      {
        dock.AttachServices(services,
          new DockSettingsHost(Dispatcher.CurrentDispatcher), () => { });
        dock.Show();
        bar.SetEnabled(true);
        var host = (StackPanel)bar.FindName("PinnedTrayHost");
        void Drop(FrameworkElement target, string id, Point point)
        {
          var payload = new DataObject(TrayIconButton.DragFormat, id);
          var args = (DragEventArgs)Activator.CreateInstance(
            typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [payload, DragDropKeyStates.None,
              DragDropEffects.Move, target, point], culture: null)!;
          args.RoutedEvent = DragDrop.DropEvent;
          target.RaiseEvent(args);
        }
        Drop(bar, first.PersistentId, new Point(10000, 10));
        Drop(bar, second.PersistentId, new Point(10000, 10));
        Assert.Equal([first.PersistentId, second.PersistentId],
          data.Layouts.Dock.TopBarItemIds);
        bar.UpdateLayout();
        Drop(bar, second.PersistentId, host.TranslatePoint(new Point(), bar));
        Assert.Equal([second.PersistentId, first.PersistentId],
          data.Layouts.Dock.TopBarItemIds);
        Assert.Equal(2, host.Children.Count);
        var loaded = new LayoutStore(data.Paths.DockLayoutFile,
          data.Paths.LauncherLayoutFile);
        loaded.LoadDock();
        Assert.Equal(data.Layouts.Dock.TopBarItemIds, loaded.Dock.TopBarItemIds);
        dock.OpenSystemTray((Button)bar.FindName("OverflowButton"), true);
        Dispatcher.CurrentDispatcher.Invoke(() => { },
          DispatcherPriority.ApplicationIdle);
        var popup = (Popup)typeof(DockWindow).GetField("tray_popup_",
          BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dock)!;
        var panel = (Border)popup.Child;
        var content = (StackPanel)((ScrollViewer)panel.Child).Content;
        var icons = content.Children.OfType<WrapPanel>().Single();
        Assert.Empty(icons.Children.OfType<TrayIconButton>());
        Drop(icons, first.PersistentId, new Point(10, 10));
        Assert.Equal([second.PersistentId], data.Layouts.Dock.TopBarItemIds);
        Assert.Single(host.Children.OfType<TrayIconButton>());
        Assert.Single(content.Children.OfType<WrapPanel>().Single()
          .Children.OfType<TrayIconButton>());
        dock.CloseSystemTray();
      }
      finally
      {
        bar.Close();
        dock.Close();
        Directory.Delete(root, true);
      }
    });
  }

  [Fact]
  public void 输入体验不出现在固定项或运行项中且普通窗口保留()
  {
    StaTest.Run(() =>
    {
      var root = Path.Combine(Path.GetTempPath(),
        "GlassDesk-input-filter-" + Guid.NewGuid().ToString("N"));
      var path = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.Windows), "SystemApps", "Example",
        "TextInputHost.exe");
      var data = new GlassDeskDataStore(new AppPaths(root));
      data.LoadAll();
      data.Library.Add(new GlassDesk.Core.Models.AppEntry
      {
        Id = "input", DisplayName = "Windows 输入体验", Target = path,
      });
      data.Layouts.Dock.PinnedItemIds.Add("input");
      using var services = new AppServiceHost(data);
      var dock = new DockWindow();
      try
      {
        dock.AttachServices(services,
          new DockSettingsHost(Dispatcher.CurrentDispatcher), () => { });
        typeof(RunningAppTracker).GetField("_windows",
          BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            services.Running, new WindowIdentityInfo[]
            {
              new(1, "Windows 输入体验", 123, false, path, null, null),
              new(2, "普通软件", 124, false, null, null, null),
            });
        dock.RefreshTiles();
        var models = (List<DockTileModel>)typeof(DockWindow).GetField("_models",
          BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dock)!;
        Assert.Equal("普通软件", Assert.Single(models).DisplayName);
        Assert.NotNull(data.Library.Find("input"));
      }
      finally
      {
        dock.Close();
        Directory.Delete(root, true);
      }
    });
  }
}
