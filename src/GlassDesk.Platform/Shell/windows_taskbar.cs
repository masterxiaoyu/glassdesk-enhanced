using System.Runtime.InteropServices;

namespace GlassDesk.Platform.Shell;

/// <summary>定位 Explorer 的主任务栏，排除本进程的托盘消息桥。</summary>
public static class WindowsTaskbar
{
  public static nint FindExplorerWindow()
  {
    nint window = 0;
    while ((window = FindWindowEx(0, window, "Shell_TrayWnd", null)) != 0)
    {
      _ = GetWindowThreadProcessId(window, out var process_id);
      if (process_id != Environment.ProcessId) return window;
    }
    return 0;
  }

  [DllImport("user32.dll", CharSet = CharSet.Unicode,
    EntryPoint = "FindWindowExW")]
  private static extern nint FindWindowEx(nint parent, nint after,
    string class_name, string? name);

  [DllImport("user32.dll")]
  private static extern uint GetWindowThreadProcessId(nint window,
    out uint process_id);
}
