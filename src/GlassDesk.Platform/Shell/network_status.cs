using System.Net.NetworkInformation;

namespace GlassDesk.Platform.Shell;

public enum NetworkConnectionKind { Disconnected, Wireless, Wired }

/// <summary>有线优先，同一时刻只向菜单栏投影一种连接状态。</summary>
public static class NetworkStatus
{
  public static NetworkConnectionKind Select(
    IEnumerable<NetworkInterfaceType> connections)
  {
    var wireless = false;
    foreach (var type in connections)
    {
      if (type is NetworkInterfaceType.Ethernet
          or NetworkInterfaceType.GigabitEthernet
          or NetworkInterfaceType.FastEthernetFx
          or NetworkInterfaceType.FastEthernetT)
        return NetworkConnectionKind.Wired;
      wireless |= type == NetworkInterfaceType.Wireless80211;
    }
    return wireless ? NetworkConnectionKind.Wireless
      : NetworkConnectionKind.Disconnected;
  }

  public static NetworkConnectionKind Read()
    => Select(NetworkInterface.GetAllNetworkInterfaces()
      .Where(item => item.OperationalStatus == OperationalStatus.Up
        && item.GetIPProperties().GatewayAddresses.Count > 0)
      .Select(item => item.NetworkInterfaceType));
}
