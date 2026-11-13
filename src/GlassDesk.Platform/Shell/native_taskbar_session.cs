using System.Runtime.InteropServices;
using GlassDesk.Platform.Displays;
using GlassDesk.Platform.Interop;

namespace GlassDesk.Platform.Shell;

/// <summary>仅在本次运行内接管任务栏可见性，释放时恢复原状态。</summary>
public sealed class NativeTaskbarSession : IDisposable
{
  private readonly HashSet<nint> hidden_windows_ = [];
  private readonly Dictionary<string, (PixelRect Original, PixelRect Released)>
    work_areas_ = [];
  private string? hide_error_;
  public bool IsHidden { get; private set; }

  public bool SetHidden(bool hidden, out string? error)
  {
    error = null;
    if (hidden == IsHidden) return true;
    if (!hidden)
    {
      Restore();
      return true;
    }

    if (WindowsTaskbar.FindExplorerWindow() == 0)
    {
      error = "Explorer 任务栏尚未就绪。";
      return false;
    }
    // 可见性与自动隐藏是两个独立功能，不改用户的自动隐藏偏好。
    hide_error_ = null;
    IsHidden = true;
    Refresh();
    if (hide_error_ is not null)
    {
      error = hide_error_;
      Restore();
      return false;
    }
    return true;
  }

  /// <summary>Explorer 重建或重新显示任务栏时，接管新的窗口。</summary>
  public void Refresh()
  {
    if (!IsHidden) return;
    var displays = DisplayService.Enumerate();
    var changed_displays = new HashSet<string>();
    var visible_windows = new List<nint>();
    EnumWindows((window, parameter) =>
    {
      var name = new char[64];
      var length = GetClassName(window, name, name.Length);
      _ = GetWindowThreadProcessId(window, out var process_id);
      if (length > 0
          && process_id != Environment.ProcessId
          && new string(name, 0, length) is
            "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
          && IsWindowVisible(window))
      {
        if (Display32.GetWindowRect(window, out var bar))
        {
          var display = displays.FirstOrDefault(item => item.Bounds.Contains(
            bar.Left + bar.Width / 2, bar.Top + bar.Height / 2));
          if (display is not null)
          {
            if (!work_areas_.ContainsKey(display.DeviceName))
            {
              changed_displays.Add(display.DeviceName);
              var area = display.WorkArea;
              var bounds = display.Bounds;
              var released = area;
              // 只释放任务栏贴边占用的区域，保留其他桌面工具栏的空间。
              if (bar.Width >= bar.Height)
              {
                if (bar.Top <= bounds.Y && area.Y == bar.Bottom)
                  released = new PixelRect(area.X, bounds.Y, area.Width,
                    area.Bottom - bounds.Y);
                else if (bar.Bottom >= bounds.Bottom && area.Bottom == bar.Top)
                  released = area with { Height = bounds.Bottom - area.Y };
              }
              else if (bar.Left <= bounds.X && area.X == bar.Right)
                released = new PixelRect(bounds.X, area.Y,
                  area.Right - bounds.X, area.Height);
              else if (bar.Right >= bounds.Right && area.Right == bar.Left)
                released = area with { Width = bounds.Right - area.X };
              work_areas_[display.DeviceName] = (area, released);
            }
          }
        }
        hidden_windows_.Add(window);
        visible_windows.Add(window);
      }
      return true;
    }, 0);
    foreach (var display in displays)
    {
      // 每块屏幕只释放一次；Explorer 重新显示栏时只隐藏，不再争抢工作区。
      if (!changed_displays.Contains(display.DeviceName)) continue;
      if (!work_areas_.TryGetValue(display.DeviceName, out var area)
          || area.Original == area.Released) continue;
      var rectangle = ChangeOwnedEdges(display.WorkArea,
        area.Original, area.Released);
      if (rectangle == display.WorkArea) continue;
      if (!DisplayService.TrySetWorkArea(rectangle))
        hide_error_ = "Windows 未允许释放任务栏工作区。";
    }
    // 修改工作区可能让 Explorer 重显任务栏，隐藏动作必须放在最后。
    foreach (var window in visible_windows)
    {
      if (!ShowWindowAsync(window, 0))
        hide_error_ = "Windows 未接受任务栏隐藏请求。";
    }
  }

  private void Restore()
  {
    IsHidden = false;
    foreach (var window in hidden_windows_)
    {
      if (IsWindow(window)) ShowWindowAsync(window, 8);
    }
    hidden_windows_.Clear();
    foreach (var display in DisplayService.Enumerate())
    {
      if (!work_areas_.TryGetValue(display.DeviceName, out var area)) continue;
      var rectangle = ChangeOwnedEdges(display.WorkArea,
        area.Released, area.Original);
      if (rectangle != display.WorkArea)
        DisplayService.TrySetWorkArea(rectangle);
    }
    work_areas_.Clear();
  }

  public void Dispose() => Restore();

  private static PixelRect ChangeOwnedEdges(PixelRect current,
    PixelRect source, PixelRect target)
  {
    // 只改变本次任务栏接管移动过的边，保留顶部栏等其他 AppBar 的调整。
    var left = source.X != target.X && current.X == source.X
      ? target.X : current.X;
    var top = source.Y != target.Y && current.Y == source.Y
      ? target.Y : current.Y;
    var right = source.Right != target.Right && current.Right == source.Right
      ? target.Right : current.Right;
    var bottom = source.Bottom != target.Bottom
      && current.Bottom == source.Bottom ? target.Bottom : current.Bottom;
    return new PixelRect(left, top, right - left, bottom - top);
  }

  private delegate bool EnumWindowCallback(nint window, nint parameter);

  [DllImport("user32.dll")]
  private static extern uint GetWindowThreadProcessId(nint window,
    out uint process_id);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool EnumWindows(EnumWindowCallback callback,
    nint parameter);

  [DllImport("user32.dll", EntryPoint = "GetClassNameW",
    CharSet = CharSet.Unicode)]
  private static extern int GetClassName(nint window, [Out] char[] name,
    int length);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool IsWindowVisible(nint window);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool IsWindow(nint window);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool ShowWindowAsync(nint window, int command);
}
