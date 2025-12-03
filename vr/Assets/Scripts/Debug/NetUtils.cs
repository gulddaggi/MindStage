// Assets/Scripts/Debug/NetUtils.cs
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using UnityEngine;

public static class NetUtils
{
    public static void LogAllLocalIPv4()
    {
        Debug.Log("=== Local IPv4 addresses (Unity) ===");
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

            var ipProps = ni.GetIPProperties();
            var gw = ipProps.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            var ips = ipProps.UnicastAddresses
                .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString());

            foreach (var ip in ips)
                Debug.Log($"IF={ni.Name} GW={(gw?.Address.ToString() ?? "-")} IP={ip}");
        }
        Debug.Log("====================================");
    }

    // “가장 그럴듯한” LAN IP(게이트웨이가 있는 IPv4)를 하나 골라 반환
    public static string GetLikelyLanIPv4()
    {
        var cand = NetworkInterface.GetAllNetworkInterfaces()
          .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                       ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                       ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
          .Select(ni => new {
              ni.Name,
              GW = ni.GetIPProperties().GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address,
              IP = ni.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address
          })
          .Where(x => x.IP != null && x.GW != null)
          .Select(x => x.IP.ToString())
          .FirstOrDefault();

        return cand ?? "0.0.0.0";
    }
}
