using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using GlassDesk.App.TopBar;
using GlassDesk.AppServices;
using GlassDesk.Core.Models.Settings;
using GlassDesk.Core.Storage;
using GlassDesk.Platform.Displays;
using GlassDesk.Platform.Shell;

namespace GlassDesk.Dock.Tests;

public class TopBarTests
{
  [Fact]
  public void 顶部栏默认关闭且开关不改变原生任务栏可见性()
  {
    Assert.False(new DockSettings().ShowTopBar);
    StaTest.Run(() =>
    {
      var root = Path.Combine(Path.GetTempPath(),
        "GlassDesk-top-bar-" + Guid.NewGuid().ToString("N"));
      var data = new GlassDeskDataStore(new AppPaths(root));
      data.LoadAll();
      using var services = new AppServiceHost(data);
      using var bridge = new SystemTrayBridge();
      Assert.True(bridge.Start());
      var bar = new TopBarWindow(services);
      var before = DisplayService.Primary()!;
      var native = WindowsTaskbar.FindExplorerWindow();
      var was_visible = IsWindowVisible(native);
      try
      {
        for (var index = 0; index < 2; index++)
        {
          bar.SetEnabled(true);
          Dispatcher.CurrentDispatcher.Invoke(() => { },
            DispatcherPriority.ApplicationIdle);
          Assert.True(bar.IsVisible);
          var enabled = DisplayService.Primary()!;
          Assert.True(enabled.WorkArea.Y >=
            before.WorkArea.Y + enabled.ToPixels(TopBarWindow.BarHeight),
            $"before={before.WorkArea}; enabled={enabled.WorkArea}; " +
            $"reserved={bar.IsWorkAreaReserved}");
          Assert.Equal(was_visible, IsWindowVisible(native));
          bar.SetEnabled(false);
          Assert.False(bar.IsVisible);
          Assert.Equal(before.WorkArea, DisplayService.Primary()!.WorkArea);
          Assert.Equal(was_visible, IsWindowVisible(native));
        }
        // 宿主切换原生可见性时先释放顶部栏，再重新占用新的工作区。
        using (var session = new NativeTaskbarSession())
        {
          Assert.True(session.SetHidden(true, out var error), error);
          var expected = DisplayService.Primary()!.WorkArea;
          bar.NativeTaskbarHidden = true;
          bar.SetEnabled(true);
          Assert.True(bar.IsWorkAreaReserved, "隐藏原生任务栏后预留顶部空间失败。");
          var handle = new WindowInteropHelper(bar).Handle;
          Assert.True(GetWindowRect(handle, out var position));
          Assert.Equal(DisplayService.Primary()!.Bounds.Y, position.Top);
          // Shell 可能保留隐藏任务栏的最小占位，但不能覆盖新栏。
          Assert.True(DisplayService.Primary()!.WorkArea.Y >=
            position.Bottom);
          // 重复刷新不能再次广播工作区变更，否则真实窗口会持续跳动。
          using var observer = new HwndSource(new HwndSourceParameters(
            "GlassDesk.WorkArea.Observer"));
          var workAreaChanges = 0;
          observer.AddHook((nint hwnd, int message, nint wparam,
            nint lparam, ref bool handled) =>
          {
            if (message == 0x001A && wparam == 0x002F) workAreaChanges++;
            return 0;
          });
          for (var index = 0; index < 6; index++)
          {
            session.Refresh();
            bar.RefreshPlacement();
            Dispatcher.CurrentDispatcher.Invoke(() => { },
              DispatcherPriority.ApplicationIdle);
          }
          Assert.Equal(0, workAreaChanges);
          bar.SetEnabled(false);
          session.Refresh();
          Thread.Sleep(100);
          var hidden = DisplayService.Primary()!;
          Assert.True(hidden.WorkArea.Y >= expected.Y);
        }
        Thread.Sleep(100);
        Assert.Equal(before.WorkArea, DisplayService.Primary()!.WorkArea);
      }
      finally
      {
        bar.Close();
        Directory.Delete(root, true);
      }
    });
  }

  [Fact]
  public void 原生应用菜单可读取子菜单状态并执行命令()
  {
    StaTest.Run(() =>
    {
      using var owner = new HwndSource(new HwndSourceParameters(
        "GlassDesk.NativeMenu.Test"));
      var received = 0;
      owner.AddHook((nint hwnd, int message, nint wparam, nint lparam,
        ref bool handled) =>
      {
        if (message == 0x0111) received = (int)wparam;
        return 0;
      });
      var menu = CreateMenu();
      var child = CreateMenu();
      try
      {
        Assert.True(AppendMenu(child, 8, 123, "选中项目"));
        Assert.True(AppendMenu(child, 2, 124, "禁用项目"));
        Assert.True(AppendMenu(menu, 0x10, (nuint)child, "文件(&F)"));
        Assert.True(SetMenu(owner.Handle, menu));
        Assert.Equal(menu, NativeApplicationMenu.ForWindow(owner.Handle));
        var top = Assert.Single(NativeApplicationMenu.Read(menu));
        Assert.Equal(child, top.Submenu);
        var entries = NativeApplicationMenu.Read(child);
        Assert.True(entries[0].Checked);
        Assert.False(entries[1].Enabled);
        Assert.True(NativeApplicationMenu.Execute(owner.Handle,
          entries[0].Command));
        Dispatcher.CurrentDispatcher.Invoke(() => { },
          DispatcherPriority.ApplicationIdle);
        Assert.Equal(123, received);
      }
      finally
      {
        SetMenu(owner.Handle, 0);
        DestroyMenu(menu);
      }
    });
  }

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool IsWindowVisible(nint window);
  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool GetWindowRect(nint window, out NativeRect rect);
  [StructLayout(LayoutKind.Sequential)]
  private struct NativeRect
  {
    public int Left, Top, Right, Bottom;
  }
  [DllImport("user32.dll")]
  private static extern nint CreateMenu();
  [DllImport("user32.dll", EntryPoint = "AppendMenuW",
    CharSet = CharSet.Unicode)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool AppendMenu(nint menu, uint flags,
    nuint item, string text);
  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool SetMenu(nint window, nint menu);
  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool DestroyMenu(nint menu);
}
