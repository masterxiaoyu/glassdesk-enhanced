using System.Runtime.InteropServices;

namespace GlassDesk.Platform.Shell;

public sealed partial class SystemTrayBridge
{
  private delegate nint WindowProcedure(nint window, uint message,
    nuint wparam, nint lparam);

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  private struct WindowClass
  {
    public uint Style;
    public nint Procedure;
    public int ClassExtra, WindowExtra;
    public nint Instance, Icon, Cursor, Background;
    public string? MenuName;
    public string ClassName;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct CopyData
  {
    public nuint Kind;
    public int Size;
    public nint Buffer;
  }

  [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW",
    CharSet = CharSet.Unicode)]
  private static extern nint GetModuleHandle(string? module);

  [DllImport("user32.dll", EntryPoint = "RegisterClassW",
    CharSet = CharSet.Unicode)]
  private static extern ushort RegisterClass(ref WindowClass window_class);

  [DllImport("user32.dll", EntryPoint = "UnregisterClassW",
    CharSet = CharSet.Unicode)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool UnregisterClass(string name, nint instance);

  [DllImport("user32.dll", EntryPoint = "CreateWindowExW",
    CharSet = CharSet.Unicode)]
  private static extern nint CreateWindowEx(uint extended_style,
    string class_name, string title, uint style, int x, int y,
    int width, int height, nint parent, nint menu, nint instance,
    nint parameter);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool SetWindowPos(nint window, nint after,
    int x, int y, int width, int height, uint flags);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool DestroyWindow(nint window);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool IsWindow(nint window);

  [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
  private static extern nint DefWindowProc(nint window, uint message,
    nuint wparam, nint lparam);

  [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
  private static extern nint SendMessageTimeout(nint window, uint message,
    nuint wparam, nint lparam, uint flags, uint timeout, out nuint result);

  [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW",
    CharSet = CharSet.Unicode)]
  private static extern uint RegisterWindowMessage(string message);

  [DllImport("user32.dll", EntryPoint = "SendNotifyMessageW")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool SendNotifyMessage(nint window, uint message,
    nuint wparam, nint lparam);

  [DllImport("user32.dll", EntryPoint = "PostMessageW")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool PostMessage(nint window, uint message,
    nuint wparam, nint lparam);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool AllowSetForegroundWindow(uint process_id);

  [DllImport("user32.dll")]
  private static extern uint GetWindowThreadProcessId(nint window,
    out uint process_id);
}
