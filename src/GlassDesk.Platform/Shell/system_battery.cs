using System.Runtime.InteropServices;

namespace GlassDesk.Platform.Shell;

public sealed record BatterySnapshot(bool HasBattery, int? Percent,
  bool? Connected, bool Charging, bool EnergySaver, uint? RemainingSeconds);

public static class SystemBattery
{
  public static BatterySnapshot? Read()
  {
    if (!GetSystemPowerStatus(out var status)) return null;
    // 255 和 uint.MaxValue 是系统定义的未知值，不显示为实际电量或时间。
    return new BatterySnapshot(status.Flags != 255
        && (status.Flags & 128) == 0,
      status.Percent <= 100 ? status.Percent : null,
      status.Ac <= 1 ? status.Ac == 1 : null,
      status.Flags != 255 && (status.Flags & 8) != 0,
      status.Saver == 1,
      status.Remaining != uint.MaxValue ? status.Remaining : null);
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct PowerStatus
  {
    public byte Ac, Flags, Percent, Saver;
    public uint Remaining, FullLifetime;
  }

  [DllImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool GetSystemPowerStatus(out PowerStatus status);
}
