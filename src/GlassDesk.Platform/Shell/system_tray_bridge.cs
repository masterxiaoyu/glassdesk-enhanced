using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using GlassDesk.Platform.Interop;

namespace GlassDesk.Platform.Shell;

/// <summary>托盘图标快照，图标像素已复制，不持有其他进程的资源句柄。</summary>
public sealed record SystemTrayItem(nint Owner, uint Id, Guid Identity,
  uint CallbackMessage, uint Version, string Title, bool Hidden,
  ExtractedIcon? Icon)
{
  public string PersistentId { get; init; } = Identity != Guid.Empty
    ? "guid:" + Identity.ToString("N") : $"session:{Owner:X}:{Id}";
}

/// <summary>
/// 接收 Shell 托盘注册消息，并转发给 Explorer 保留原生托盘和通知。
/// 必须在有消息循环的 UI 线程创建、刷新和释放。
/// </summary>
public sealed partial class SystemTrayBridge : IDisposable
{
  private static nint active_window_;
  private readonly Dictionary<(nint, uint), SystemTrayItem> items_ = [];
  private readonly WindowProcedure procedure_;
  private readonly nint instance_;
  private nint window_;
  private readonly uint taskbar_created_;

  public event EventHandler? Changed;
  public IReadOnlyList<SystemTrayItem> Items => items_.Values.ToArray();
  public bool IsRunning => window_ != 0;

  public SystemTrayBridge()
  {
    procedure_ = ProcessMessage;
    instance_ = GetModuleHandle(null);
    taskbar_created_ = RegisterWindowMessage("TaskbarCreated");
  }

  public bool Start()
  {
    if (window_ != 0) return true;
    var window_class = new WindowClass
    {
      Procedure = Marshal.GetFunctionPointerForDelegate(procedure_),
      Instance = instance_,
      ClassName = "Shell_TrayWnd",
    };
    if (RegisterClass(ref window_class) == 0) return false;
    window_ = CreateWindowEx(0x88, "Shell_TrayWnd", "GlassDesk.TrayBridge",
      0x80000000, 0, 0, 0, 0, 0, 0, instance_, 0);
    if (window_ == 0)
    {
      UnregisterClass("Shell_TrayWnd", instance_);
      return false;
    }
    Refresh();
    active_window_ = window_;
    // 托盘重建的标准通知让已经运行的应用重新提交图标。
    BroadcastTaskbarCreated();
    return true;
  }

  public void Refresh()
  {
    if (window_ == 0) return;
    // FindWindow 按 Z 序寻找 Shell_TrayWnd；桥窗口保持隐藏且不激活。
    SetWindowPos(window_, -1, 0, 0, 0, 0, 0x13);
    var removed = false;
    foreach (var pair in items_.ToArray())
    {
      if (IsWindow(pair.Key.Item1)) continue;
      items_.Remove(pair.Key);
      removed = true;
    }
    if (removed) Changed?.Invoke(this, EventArgs.Empty);
  }

  private nint ProcessMessage(nint window, uint message,
    nuint wparam, nint lparam)
  {
    var accepted = false;
    if (message == 0x004A && lparam != 0)
    {
      var data = Marshal.PtrToStructure<CopyData>(lparam);
      // 私有 Shell 线格式由入口统一校验；不按发送进程的指针宽度解析。
      if (data.Kind == 1 && data.Size is >= 964 and <= 4096
          && data.Buffer != 0)
      {
        var bytes = new byte[data.Size];
        Marshal.Copy(data.Buffer, bytes, 0, bytes.Length);
        accepted = ApplyPacket(bytes);
      }
    }

    if (message is 0x004A or 0x0111 or 0x0112
        || message >= 0x0400 && message != taskbar_created_)
    {
      var explorer = WindowsTaskbar.FindExplorerWindow();
      // WM_COPYDATA 的内存仅在回调期间有效，必须同步转发并设超时。
      if (explorer != 0 && SendMessageTimeout(explorer, message, wparam,
          lparam, 2, 200, out var result) != 0)
        return accepted ? 1 : (nint)result;
      return accepted ? 1 : 0;
    }
    return DefWindowProc(window, message, wparam, lparam);
  }

  private bool ApplyPacket(byte[] packet)
  {
    var span = packet.AsSpan();
    if (BinaryPrimitives.ReadUInt32LittleEndian(span) != 0x34753423)
      return false;
    var command = BinaryPrimitives.ReadUInt32LittleEndian(span[4..]);
    var owner = (nint)BinaryPrimitives.ReadUInt32LittleEndian(span[12..]);
    var id = BinaryPrimitives.ReadUInt32LittleEndian(span[16..]);
    var flags = BinaryPrimitives.ReadUInt32LittleEndian(span[20..]);
    var guid = (flags & 0x20) != 0 ? new Guid(span.Slice(944, 16))
      : Guid.Empty;
    var key = (owner, id);
    if (guid != Guid.Empty)
    {
      var existing = items_.FirstOrDefault(pair => pair.Value.Identity == guid);
      if (existing.Value is not null) key = existing.Key;
    }
    if (command == 2)
    {
      if (items_.Remove(key)) Changed?.Invoke(this, EventArgs.Empty);
      return true;
    }
    if (command is not (0 or 1 or 4) || !IsWindow(owner)) return false;
    items_.TryGetValue(key, out var previous);
    if (command == 4)
    {
      if (previous is null) return false;
      items_[key] = previous with
      {
        Version = BinaryPrimitives.ReadUInt32LittleEndian(span[808..]),
      };
      Changed?.Invoke(this, EventArgs.Empty);
      return true;
    }
    var callback = (flags & 1) != 0
      ? BinaryPrimitives.ReadUInt32LittleEndian(span[24..])
      : previous?.CallbackMessage ?? 0;
    var title = (flags & 4) != 0
      ? Encoding.Unicode.GetString(span.Slice(32, 256)).Split('\0')[0]
      : previous?.Title ?? string.Empty;
    var icon = previous?.Icon;
    if ((flags & 2) != 0)
    {
      var handle = (nint)BinaryPrimitives.ReadUInt32LittleEndian(span[28..]);
      icon = handle == 0 ? null : IconExtractor.FromIconHandle(handle);
    }
    var hidden = previous?.Hidden ?? false;
    if ((flags & 8) != 0
        && (BinaryPrimitives.ReadUInt32LittleEndian(span[292..]) & 1) != 0)
      hidden = (BinaryPrimitives.ReadUInt32LittleEndian(span[288..]) & 1) != 0;
    items_.Remove(key);
    var persistent_id = previous?.PersistentId;
    if (persistent_id is null)
    {
      _ = GetWindowThreadProcessId(owner, out var process_id);
      var path = WindowLocator.TryReadImagePath(process_id);
      // 不保存进程号或窗口句柄，应用重启后仍能认出同一枚托盘图标。
      persistent_id = guid != Guid.Empty ? "guid:" + guid.ToString("N")
        : path is not null ? $"app:{path.ToUpperInvariant()}:{id}"
        : $"session:{owner:X}:{id}";
    }
    items_[(owner, id)] = new SystemTrayItem(owner, id, guid == Guid.Empty
      ? previous?.Identity ?? Guid.Empty : guid, callback,
      previous?.Version ?? 0, title, hidden, icon)
    {
      PersistentId = persistent_id,
    };
    Changed?.Invoke(this, EventArgs.Empty);
    return true;
  }

  /// <summary>转发真实的应用回调，版本 4 携带坐标和图标编号。</summary>
  public static bool SendCallback(SystemTrayItem item, uint message,
    int x, int y)
  {
    if (!IsWindow(item.Owner) || item.CallbackMessage < 0x400) return false;
    _ = GetWindowThreadProcessId(item.Owner, out var process_id);
    AllowSetForegroundWindow(process_id);
    var coordinates = (uint)(ushort)x | ((uint)(ushort)y << 16);
    var wparam = item.Version >= 4 ? coordinates : item.Id;
    var lparam = item.Version >= 4 ? message | (item.Id << 16) : message;
    return PostMessage(item.Owner, item.CallbackMessage, wparam, (nint)lparam);
  }

  private void BroadcastTaskbarCreated()
  {
    if (taskbar_created_ != 0)
      SendNotifyMessage(0xFFFF, taskbar_created_, 0, 0);
  }

  public void Dispose()
  {
    if (window_ == 0) return;
    active_window_ = 0;
    DestroyWindow(window_);
    window_ = 0;
    UnregisterClass("Shell_TrayWnd", instance_);
    items_.Clear();
    // 退出桥后，让应用重新把图标注册给仍在运行的 Explorer。
    BroadcastTaskbarCreated();
  }

  internal static nuint SendAppBarMessage(uint message,
    ref Shell32.AppBarData data)
  {
    // 状态查询没有共享数据输出，保持高频后台查询不改变窗口顺序。
    if (message == Shell32.AbmGetState)
      return Shell32.SHAppBarMessageNative(message, ref data);
    // AppBar 使用面向目标进程的共享内存，不能像托盘图标那样原样转发。
    // 调用期间让 Shell 直接找到 Explorer，随后恢复图标消息桥的顺序。
    var bridge = active_window_;
    if (bridge != 0) SetWindowPos(bridge, 1, 0, 0, 0, 0, 0x13);
    var result = Shell32.SHAppBarMessageNative(message, ref data);
    if (bridge != 0) SetWindowPos(bridge, -1, 0, 0, 0, 0, 0x13);
    return result;
  }
}
