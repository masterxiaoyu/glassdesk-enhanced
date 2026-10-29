using System.Runtime.InteropServices;

using GlassDesk.Platform.Interop;

namespace GlassDesk.Platform.Shell;

/// <summary>通过 Windows Shell 公共接口读取任务栏并切换自动隐藏。</summary>
public static partial class TaskbarSystemSettings
{
  /// <summary>读取系统任务栏的自动隐藏状态；Explorer 不在时返回 null。</summary>
  public static bool? ReadAutoHide()
  {
    if (FindWindow("Shell_TrayWnd", null) == 0) return null;
    var data = new Shell32.AppBarData
    {
      Size = (uint)Marshal.SizeOf<Shell32.AppBarData>(),
    };
    var state = Shell32.SHAppBarMessage(Shell32.AbmGetState, ref data);
    return (state & Shell32.AbsAutoHide) != 0;
  }

  /// <summary>读取系统任务栏位置；可选位置由 Windows 版本和推送决定。</summary>
  public static string? ReadPosition()
  {
    var data = new Shell32.AppBarData
    {
      Size = (uint)Marshal.SizeOf<Shell32.AppBarData>(),
    };
    if (Shell32.SHAppBarMessage(Shell32.AbmGetTaskbarPos, ref data) == 0)
      return null;
    return data.Edge switch
    {
      0 => "左侧",
      1 => "顶部",
      2 => "右侧",
      3 => "底部",
      _ => null,
    };
  }

  /// <summary>仅在用户操作后写入系统设置，并读回确认是否生效。</summary>
  public static bool TrySetAutoHide(bool enabled, out string? error)
  {
    error = null;
    var window = FindWindow("Shell_TrayWnd", null);
    if (window == 0)
    {
      error = "Explorer 任务栏尚未就绪。";
      return false;
    }

    var data = new Shell32.AppBarData
    {
      Size = (uint)Marshal.SizeOf<Shell32.AppBarData>(),
      Window = window,
    };
    var old_state = Shell32.SHAppBarMessage(Shell32.AbmGetState, ref data);
    if (((old_state & Shell32.AbsAutoHide) != 0) == enabled) return true;

    // 保留 Shell 返回的置顶标志，只更改自动隐藏位。
    var new_state = old_state & Shell32.AbsAlwaysOnTop;
    if (enabled) new_state |= Shell32.AbsAutoHide;
    data.Parameter = (nint)new_state;
    Shell32.SHAppBarMessage(Shell32.AbmSetState, ref data);
    if (ReadAutoHide() == enabled) return true;

    error = "Windows 未接受自动隐藏设置，请在系统任务栏设置中调整。";
    return false;
  }

  [LibraryImport("user32.dll", EntryPoint = "FindWindowW",
    StringMarshalling = StringMarshalling.Utf16)]
  private static partial nint FindWindow(string class_name,
    string? window_name);
}
