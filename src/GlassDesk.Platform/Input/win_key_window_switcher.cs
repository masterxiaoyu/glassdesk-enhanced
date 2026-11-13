using System.Runtime.InteropServices;
using GlassDesk.Platform.Shell;

namespace GlassDesk.Platform.Input;

/// <summary>在消息循环线程上监听单击 Win；退出时卸载钩子。</summary>
public sealed class WinKeyWindowSwitcher : IDisposable
{
  internal const nuint InputMarker = 0x4744574B;
  private readonly Action switch_windows_;
  private readonly HookProc callback_;
  private readonly WinKeyGesture gesture_ = new();
  private nint hook_;

  public WinKeyWindowSwitcher(Action switchWindows)
  {
    switch_windows_ = switchWindows;
    callback_ = OnKeyboard;
  }

  public bool TryStart(out int error)
  {
    hook_ = SetWindowsHookEx(13, callback_, GetModuleHandle(null), 0);
    error = hook_ == 0 ? Marshal.GetLastWin32Error() : 0;
    return hook_ != 0;
  }

  private nint OnKeyboard(int code, nuint message, nint data)
  {
    if (code < 0) return CallNextHookEx(hook_, code, message, data);
    var input = Marshal.PtrToStructure<KeyboardInput>(data);
    // 只跳过自己重放的事件，屏幕键盘等其他输入仍遵循相同规则。
    if (input.ExtraInfo == InputMarker)
      return CallNextHookEx(hook_, code, message, data);
    var up = (input.Flags & 0x80) != 0;
    var modifiers = (GetAsyncKeyState(0x10) & 0x8000) != 0
      || (GetAsyncKeyState(0x11) & 0x8000) != 0
      || (GetAsyncKeyState(0x12) & 0x8000) != 0;
    switch (gesture_.Process(input.Key, up, modifiers))
    {
      case WinKeyDecision.Suppress:
        return 1;
      case WinKeyDecision.SwitchWindows:
        switch_windows_();
        return 1;
      case WinKeyDecision.ReplayChord:
        var keys = new List<(ushort Key, uint Flags)>();
        if ((gesture_.ForwardedWindowsKeys & 1) != 0)
          keys.Add((0x5B, 1));
        if ((gesture_.ForwardedWindowsKeys & 2) != 0)
          keys.Add((0x5C, 1));
        keys.Add(((ushort)input.Key, input.Flags & 1));
        return WindowsShellCommands.SendKeyboardInputs(keys.ToArray())
          ? 1 : CallNextHookEx(hook_, code, message, data);
      default:
        return CallNextHookEx(hook_, code, message, data);
    }
  }

  public void Dispose()
  {
    if (hook_ == 0) return;
    UnhookWindowsHookEx(hook_);
    hook_ = 0;
    // 开关关闭时若组合键尚未松开，归还已经重放过的 Win 状态。
    if ((gesture_.ForwardedWindowsKeys & 1) != 0)
      WindowsShellCommands.SendKeyboardInputs((0x5B, 3));
    if ((gesture_.ForwardedWindowsKeys & 2) != 0)
      WindowsShellCommands.SendKeyboardInputs((0x5C, 3));
    GC.KeepAlive(callback_);
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct KeyboardInput
  {
    public uint Key, ScanCode, Flags, Time;
    public nuint ExtraInfo;
  }

  private delegate nint HookProc(int code, nuint message, nint data);

  [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW",
    SetLastError = true)]
  private static extern nint SetWindowsHookEx(int type, HookProc callback,
    nint module, uint thread);
  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool UnhookWindowsHookEx(nint hook);
  [DllImport("user32.dll")]
  private static extern nint CallNextHookEx(nint hook, int code,
    nuint message, nint data);
  [DllImport("user32.dll")]
  private static extern short GetAsyncKeyState(int key);
  [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW",
    CharSet = CharSet.Unicode)]
  private static extern nint GetModuleHandle(string? name);
}
