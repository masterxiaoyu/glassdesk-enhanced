using System.Runtime.InteropServices;

namespace GlassDesk.Platform.Shell;

/// <summary>调用 Windows 原生开始菜单和系统浮层。</summary>
public static class WindowsShellCommands
{
  public static bool OpenStart()
  {
    var taskbar = WindowsTaskbar.FindExplorerWindow();
    // SC_TASKLIST 请求系统开始菜单，菜单内容与搜索框继续由 Explorer 管理。
    return taskbar != 0 && PostMessage(taskbar, 0x0112, 0xF130, 0);
  }

  public static bool OpenQuickSettings() => SendWindowsKey(0x41);
  public static bool OpenNotifications() => SendWindowsKey(0x4E);

  /// <summary>保持窗口缩略图界面展开，松开按键后仍可选择窗口。</summary>
  public static bool OpenWindowSwitcher() => SendKeyboardInputs(
    (0x11, 0), (0x12, 0), (0x09, 0),
    (0x09, 2), (0x12, 2), (0x11, 2));

  private static bool SendWindowsKey(ushort key)
  {
    // 一次投递完整的按下/抬起序列，不留下按住的修饰键。
    return SendKeyboardInputs((0x5B, 1), (key, 0), (key, 2), (0x5B, 3));
  }

  internal static bool SendKeyboardInputs(
    params (ushort Key, uint Flags)[] keys)
  {
    var inputs = keys.Select(key => new Input
    {
      Type = 1,
      Key = new KeyInput
      {
        VirtualKey = key.Key, Flags = key.Flags,
        ExtraInfo = GlassDesk.Platform.Input.WinKeyWindowSwitcher.InputMarker,
      },
    }).ToArray();
    return SendInput((uint)inputs.Length, inputs,
      Marshal.SizeOf<Input>()) == inputs.Length;
  }

  // INPUT 的联合体必须为 MOUSEINPUT 留足空间，兼容 x64 和 x86 对齐。
  [StructLayout(LayoutKind.Sequential)]
  private struct Input
  {
    public uint Type;
    public InputUnion Data;
    public KeyInput Key { set => Data.Keyboard = value; }
  }

  [StructLayout(LayoutKind.Explicit)]
  private struct InputUnion
  {
    [FieldOffset(0)] public KeyInput Keyboard;
    [FieldOffset(0)] public MouseInput Mouse;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct KeyInput
  {
    public ushort VirtualKey, ScanCode;
    public uint Flags, Time;
    public nuint ExtraInfo;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct MouseInput
  {
    public int X, Y;
    public uint MouseData, Flags, Time;
    public nuint ExtraInfo;
  }

  [DllImport("user32.dll")]
  private static extern uint SendInput(uint count, Input[] inputs, int size);

  [DllImport("user32.dll", EntryPoint = "PostMessageW")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool PostMessage(nint window, uint message,
    nuint wparam, nint lparam);

}
