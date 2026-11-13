using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using GlassDesk.Platform;
using GlassDesk.Platform.Shell;
using GlassDesk.Platform.Displays;

namespace GlassDesk.Dock.Tests;

public class SystemTrayBridgeTests
{
  [Fact]
  public void 原生任务栏接管后恢复原自动隐藏状态()
  {
    StaTest.Run(() =>
    {
      var original = TaskbarSystemSettings.ReadAutoHide();
      Assert.NotNull(original);
      var displays = DisplayService.Enumerate();
      var taskbar = WindowsTaskbar.FindExplorerWindow();
      var was_visible = IsWindowVisible(taskbar);
      using var bridge = new SystemTrayBridge();
      Assert.True(bridge.Start());
      Assert.Equal(original, TaskbarSystemSettings.ReadAutoHide());
      using (var session = new NativeTaskbarSession())
      {
        Assert.True(session.SetHidden(true, out var error), error);
        Assert.True(session.IsHidden);
        Thread.Sleep(100);
        Assert.False(IsWindowVisible(taskbar), ShellWindowState());
        Assert.Equal(original, TaskbarSystemSettings.ReadAutoHide());
        var primary = DisplayService.Primary()!;
        Assert.Equal(primary.Bounds.Bottom, primary.WorkArea.Bottom);
      }
      Thread.Sleep(100);
      Assert.Equal(original, TaskbarSystemSettings.ReadAutoHide());
      Assert.Equal(was_visible, IsWindowVisible(taskbar));
      foreach (var display in DisplayService.Enumerate())
        Assert.Equal(displays.Single(item =>
          item.DeviceName == display.DeviceName).WorkArea, display.WorkArea);
    });
  }

  [Fact]
  public void 真实Shell图标支持版本四坐标及隐藏状态()
  {
    StaTest.Run(() =>
    {
      using var owner = new HwndSource(new HwndSourceParameters(
        "GlassDesk.TrayBridge.V4.Test") { ParentWindow = -3 });
      var callbacks = new List<(nint, nint)>();
      owner.AddHook((nint window, int message, nint wparam, nint lparam,
        ref bool handled) =>
      {
        if (message == 0x8001) callbacks.Add((wparam, lparam));
        return 0;
      });
      using var bridge = new SystemTrayBridge();
      Assert.True(bridge.Start());
      var data = new NativeIconData
      {
        Size = Marshal.SizeOf<NativeIconData>(), Owner = owner.Handle,
        Id = 456, Flags = 0x27, Callback = 0x8001, Icon = CreateIcon(),
        Tip = "版本四测试", Info = "", InfoTitle = "",
        Identity = Guid.NewGuid(),
      };
      try
      {
        Assert.True(NotifyIcon(0, ref data));
        data.Version = 4;
        Assert.True(NotifyIcon(4, ref data));
        Assert.True(bridge.Items.Count > 0, ShellWindowState());
        var item = Assert.Single(bridge.Items, entry =>
          entry.Identity == data.Identity);
        Assert.Equal(4u, item.Version);
        Assert.True(SystemTrayBridge.SendCallback(item, 0x7B, -100, 200));
        Dispatcher.CurrentDispatcher.Invoke(() => { },
          DispatcherPriority.ApplicationIdle);
        var coordinates = (uint)unchecked((ushort)-100)
          | (200u << 16);
        Assert.Contains(((nint)coordinates, (nint)(0x7Bu | (456u << 16))),
          callbacks);
        data.Flags = 0x28;
        data.State = data.StateMask = 1;
        Assert.True(NotifyIcon(1, ref data));
        Assert.True(Assert.Single(bridge.Items, entry =>
          entry.Identity == data.Identity).Hidden);
      }
      finally
      {
        NotifyIcon(2, ref data);
        IconFactory.DestroyIcon(data.Icon);
      }
    });
  }

  [Fact]
  public void 真实Shell图标支持增改删及版本三回调()
  {
    StaTest.Run(() =>
    {
      using var owner = new HwndSource(new HwndSourceParameters(
        "GlassDesk.TrayBridge.Test") { ParentWindow = -3 });
      var callbacks = new List<(nuint, nint)>();
      owner.AddHook((nint window, int message, nint wparam, nint lparam,
        ref bool handled) =>
      {
        if (message == TrayIcon.CallbackMessage)
          callbacks.Add(((nuint)wparam, lparam));
        return 0;
      });
      using var bridge = new SystemTrayBridge();
      Assert.True(bridge.Start());
      using var icon = new TrayIcon(owner.Handle, 123);
      Assert.True(icon.Show(CreateIcon(), "桥接测试图标"));
      var item = Assert.Single(bridge.Items, entry =>
        entry.Owner == owner.Handle && entry.Id == 123);
      Assert.Equal("桥接测试图标", item.Title);
      Assert.Equal(3u, item.Version);
      Assert.NotNull(item.Icon);
      Assert.True(item.Icon.VisiblePixelCount > 0);
      Assert.True(icon.Update(CreateIcon(), "修改后的图标"));
      item = Assert.Single(bridge.Items, entry =>
        entry.Owner == owner.Handle && entry.Id == 123);
      Assert.Equal("修改后的图标", item.Title);
      Assert.True(SystemTrayBridge.SendCallback(item, 0x0202, 100, 200));
      Dispatcher.CurrentDispatcher.Invoke(() => { },
        DispatcherPriority.ApplicationIdle);
      Assert.Contains(((nuint)123, (nint)0x0202), callbacks);
      icon.Hide();
      Assert.DoesNotContain(bridge.Items, entry =>
        entry.Owner == owner.Handle && entry.Id == 123);
    });
  }

  private static nint CreateIcon()
  {
    var pixels = Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray();
    return IconFactory.CreateIconFromBgra(pixels, 16, 16);
  }

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  private struct NativeIconData
  {
    public int Size;
    public nint Owner;
    public uint Id, Flags, Callback;
    public nint Icon;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
    public uint State, StateMask;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
    public uint Version;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
    public uint InfoFlags;
    public Guid Identity;
    public nint Balloon;
  }

  [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW",
    CharSet = CharSet.Unicode)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool NotifyIcon(uint command, ref NativeIconData data);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool IsWindowVisible(nint window);

  private static string ShellWindowState()
  {
    var windows = new List<string>();
    nint window = 0;
    while ((window = FindWindowEx(0, window, "Shell_TrayWnd", null)) != 0)
    {
      _ = GetWindowThreadProcessId(window, out var id);
      using var process = System.Diagnostics.Process.GetProcessById((int)id);
      windows.Add($"{process.ProcessName}:{id}:visible={IsWindowVisible(window)}");
    }
    return string.Join("; ", windows);
  }

  [DllImport("user32.dll", EntryPoint = "FindWindowExW",
    CharSet = CharSet.Unicode)]
  private static extern nint FindWindowEx(nint parent, nint after,
    string name, string? title);

  [DllImport("user32.dll")]
  private static extern uint GetWindowThreadProcessId(nint window, out uint id);
}
