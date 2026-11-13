namespace GlassDesk.Platform.Shell;

/// <summary>系统状态已有独立入口，不再混入应用托盘图标。</summary>
public static class SystemStatusIcons
{
  private static readonly Guid kVolume =
    new("7820ae73-23e3-4229-82c1-e41cb67d5b9c");
  private static readonly Guid kVolumeAlternate =
    new("7820ae76-23e3-4229-82c1-e41cb67d5b9c");
  private static readonly Guid kNetwork =
    new("05438b20-0a9a-4c7d-bb4e-a26f4bca7eab");
  private static readonly Guid kPower =
    new("7820ae75-23e3-4229-82c1-e41cb67d5b9c");
  private static readonly Guid kPowerAlternate =
    new("025a5937-a6be-4686-a844-36fe4bec8b6d");

  public static bool IsDedicated(SystemTrayItem item)
    // 包含本机 Shell 实际注册的音量与电源标识，不依赖本地化提示文字。
    => item.Identity == kVolume || item.Identity == kVolumeAlternate
      || item.Identity == kNetwork || item.Identity == kPower
      || item.Identity == kPowerAlternate;
}
