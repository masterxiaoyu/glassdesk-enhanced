using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using GlassDesk.Platform;

namespace GlassDesk.Compatibility;

internal static class TaskbarFixture
{
  internal static int Run(string root)
  {
    var app = new Application();
    var window = new Window
    {
      Title = "GlassDesk 任务栏测试窗口", Width = 320, Height = 160,
      ShowInTaskbar = true,
    };
    window.Show();
    var handle = new WindowInteropHelper(window).Handle;
    using var icon = new TrayIcon(handle, 173);
    void register_icon() => icon.Show(IconFactory.CreateIconFromBgra(
      Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray(), 16, 16),
      "GlassDesk 任务栏测试托盘");
    register_icon();
    var created = RegisterWindowMessage("TaskbarCreated");
    HwndSource.FromHwnd(handle)!.AddHook((nint hwnd, int message,
      nint wparam, nint lparam, ref bool handled) =>
    {
      if ((uint)message == created) register_icon();
      if (message == TrayIcon.CallbackMessage)
        File.WriteAllText(Path.Combine(root, "callback.txt"),
          lparam.ToString(CultureInfo.InvariantCulture));
      return 0;
    });
    File.WriteAllText(Path.Combine(root, "ready.txt"),
      handle.ToString(CultureInfo.InvariantCulture));
    var timer = new DispatcherTimer
    {
      Interval = TimeSpan.FromMilliseconds(30),
    };
    var previous = "";
    timer.Tick += (_, _) =>
    {
      var path = Path.Combine(root, "command.txt");
      if (!File.Exists(path)) return;
      var command = File.ReadAllText(path);
      if (command == previous) return;
      previous = command;
      if (command == "title") window.Title = "GlassDesk 已更新标题";
      if (command == "exit") app.Shutdown();
      File.WriteAllText(Path.Combine(root, "ack.txt"), command);
    };
    timer.Start();
    app.Run();
    return 0;
  }

  [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW",
    CharSet = CharSet.Unicode)]
  private static extern uint RegisterWindowMessage(string message);
}
