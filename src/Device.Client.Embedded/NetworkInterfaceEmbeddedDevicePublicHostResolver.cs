using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Device.Client.Embedded;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
// DOCS: docs/handoffs/2026-04-22-device-client-embedded-adapter.md
public sealed class NetworkInterfaceEmbeddedDevicePublicHostResolver : IEmbeddedDevicePublicHostResolver
{
    private static readonly string[] VirtualAdapterKeywords =
    {
        "virtual",
        "vethernet",
        "hyper-v",
        "docker",
        "wsl",
        "vmware",
        "virtualbox",
        "loopback",
        "tunnel",
        "tap"
    };

    public string ResolvePublicHost()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            var candidates = GetIpv4Candidates(interfaces.Where(IsPreferredPhysicalAdapter));
            if (candidates.Length == 0)
            {
                candidates = GetIpv4Candidates(interfaces.Where(IsUsableAdapter).Where(nic => !IsLikelyVirtualAdapter(nic)));
            }

            if (candidates.Length == 0)
            {
                return "127.0.0.1";
            }

            var privateIp = candidates.FirstOrDefault(IsPrivateIpv4);
            return privateIp ?? candidates[0];
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    private static string[] GetIpv4Candidates(IEnumerable<NetworkInterface> interfaces)
    {
        return interfaces
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .Where(info => info.Address.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(info.Address))
            .Select(info => info.Address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsPreferredPhysicalAdapter(NetworkInterface nic)
    {
        if (!IsUsableAdapter(nic) || IsLikelyVirtualAdapter(nic))
        {
            return false;
        }

        return nic.NetworkInterfaceType is NetworkInterfaceType.Wireless80211
            or NetworkInterfaceType.Ethernet
            or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.FastEthernetFx
            or NetworkInterfaceType.FastEthernetT;
    }

    private static bool IsUsableAdapter(NetworkInterface nic)
    {
        return nic.OperationalStatus == OperationalStatus.Up
            && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback
            && nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel;
    }

    private static bool IsLikelyVirtualAdapter(NetworkInterface nic)
    {
        var descriptor = $"{nic.Name} {nic.Description}";
        return VirtualAdapterKeywords.Any(keyword => descriptor.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPrivateIpv4(string candidate)
    {
        if (!IPAddress.TryParse(candidate, out var address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
