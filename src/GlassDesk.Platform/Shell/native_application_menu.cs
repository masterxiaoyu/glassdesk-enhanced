using System.Runtime.InteropServices;

namespace GlassDesk.Platform.Shell;

public sealed record NativeMenuEntry(string Text, uint Command,
  nint Submenu, bool Enabled, bool Checked, bool Separator);

/// <summary>读取前台应用实际提供的 Win32 菜单，不生成不可执行的占位菜单。</summary>
public static class NativeApplicationMenu
{
  public static nint ForWindow(nint window) => GetMenu(window);

  public static IReadOnlyList<NativeMenuEntry> Read(nint menu)
  {
    var items = new List<NativeMenuEntry>();
    var count = GetMenuItemCount(menu);
    for (var index = 0; index < count; index++)
    {
      var text = new char[256];
      var length = GetMenuString(menu, (uint)index, text, text.Length, 0x400);
      var state = GetMenuState(menu, (uint)index, 0x400);
      if (state == uint.MaxValue) continue;
      items.Add(new NativeMenuEntry(new string(text, 0, length),
        GetMenuItemID(menu, index), GetSubMenu(menu, index),
        (state & 3) == 0, (state & 8) != 0, (state & 0x800) != 0));
    }
    return items;
  }

  public static void Initialize(nint window, nint menu, int index)
    => SendMessageTimeout(window, 0x0117, (nuint)menu, index,
      2, 200, out _);

  public static bool Execute(nint window, uint command)
  {
    if (command == uint.MaxValue || !WindowControl.IsAlive(window))
      return false;
    WindowLocator.TryBringToForeground(window);
    return PostMessage(window, 0x0111, command, 0);
  }

  [DllImport("user32.dll")]
  private static extern nint GetMenu(nint window);
  [DllImport("user32.dll")]
  private static extern int GetMenuItemCount(nint menu);
  [DllImport("user32.dll", EntryPoint = "GetMenuStringW",
    CharSet = CharSet.Unicode)]
  private static extern int GetMenuString(nint menu, uint item,
    [Out] char[] text, int size, uint flags);
  [DllImport("user32.dll")]
  private static extern uint GetMenuState(nint menu, uint item, uint flags);
  [DllImport("user32.dll")]
  private static extern uint GetMenuItemID(nint menu, int index);
  [DllImport("user32.dll")]
  private static extern nint GetSubMenu(nint menu, int index);
  [DllImport("user32.dll", EntryPoint = "PostMessageW")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool PostMessage(nint window, uint message,
    nuint wparam, nint lparam);
  [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
  private static extern nint SendMessageTimeout(nint window, uint message,
    nuint wparam, nint lparam, uint flags, uint timeout, out nuint result);
}
