using System.Runtime.InteropServices;

namespace GlassDesk.Platform.Shell;

/// <summary>独立音量面板使用默认播放设备，关闭面板即释放 COM 引用。</summary>
public sealed class SystemVolume : IDisposable
{
  private IEndpointVolume? endpoint_;

  private SystemVolume(IEndpointVolume endpoint) => endpoint_ = endpoint;

  public static SystemVolume? Open()
  {
    var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    var iid = typeof(IDeviceEnumerator).GUID;
    if (CoCreateInstance(in clsid, 0, 23, in iid, out var devices) < 0)
      return null;
    IDevice? device = null;
    try
    {
      // eRender / eMultimedia：与系统主音量控制同一个默认输出设备。
      if (devices.GetDefaultAudioEndpoint(0, 1, out device) < 0) return null;
      var volumeId = typeof(IEndpointVolume).GUID;
      return device.Activate(in volumeId, 23, 0, out var volume) >= 0
        ? new SystemVolume(volume) : null;
    }
    finally
    {
      if (device is not null) Marshal.ReleaseComObject(device);
      Marshal.ReleaseComObject(devices);
    }
  }

  public bool TryRead(out float value, out bool muted)
  {
    value = 0;
    muted = false;
    return endpoint_ is { } endpoint
      && endpoint.GetMasterVolumeLevelScalar(out value) >= 0
      && endpoint.GetMute(out muted) >= 0;
  }

  public bool SetLevel(float value)
    => endpoint_?.SetMasterVolumeLevelScalar(value, 0) >= 0;

  public bool SetMuted(bool muted) => endpoint_?.SetMute(muted, 0) >= 0;

  public void Dispose()
  {
    if (endpoint_ is null) return;
    Marshal.ReleaseComObject(endpoint_);
    endpoint_ = null;
  }

  [DllImport("ole32.dll", ExactSpelling = true)]
  private static extern int CoCreateInstance(in Guid clsid, nint outer,
    uint context, in Guid iid,
    [MarshalAs(UnmanagedType.Interface)] out IDeviceEnumerator devices);

  [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
   InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  private interface IDeviceEnumerator
  {
    [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out nint devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role,
      out IDevice device);
  }

  [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
   InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  private interface IDevice
  {
    [PreserveSig] int Activate(in Guid iid, uint context, nint parameters,
      [MarshalAs(UnmanagedType.Interface)] out IEndpointVolume volume);
  }

  // 保持原生接口的槽位顺序，未使用的前置方法也必须声明。
  [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
   InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  private interface IEndpointVolume
  {
    [PreserveSig] int RegisterControlChangeNotify(nint callback);
    [PreserveSig] int UnregisterControlChangeNotify(nint callback);
    [PreserveSig] int GetChannelCount(out uint count);
    [PreserveSig] int SetMasterVolumeLevel(float value, nint context);
    [PreserveSig] int SetMasterVolumeLevelScalar(float value, nint context);
    [PreserveSig] int GetMasterVolumeLevel(out float value);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float value);
    [PreserveSig] int SetChannelVolumeLevel(uint channel, float value,
      nint context);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float value,
      nint context);
    [PreserveSig] int GetChannelVolumeLevel(uint channel, out float value);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint channel,
      out float value);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted,
      nint context);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
  }
}
