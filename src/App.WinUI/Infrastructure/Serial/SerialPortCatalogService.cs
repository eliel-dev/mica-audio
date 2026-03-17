using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

namespace App.WinUI.Infrastructure.Serial;

// DOCS: docs/wiki/guides/setup-new-device.md#passos
internal sealed class SerialPortCatalogService : ISerialPortCatalogService
{
    private static readonly string[] PreferredVids = ["303A", "10C4", "1A86", "0403"];
    private static readonly Regex ComPortRegex = new(@"\((COM\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VidPidRegex = new(@"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PortNumberRegex = new(@"COM(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<IReadOnlyList<SerialPortDescriptor>> ListAsync(bool includeAllPorts, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var metadataByPort = TryLoadMetadataByPort();
        var knownPorts = SerialPort.GetPortNames()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var list = new List<SerialPortDescriptor>(knownPorts.Length);
        foreach (var port in knownPorts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            metadataByPort.TryGetValue(port, out var metadata);
            var descriptor = BuildDescriptor(port, metadata);
            list.Add(descriptor);
        }

        list.Sort(CompareDescriptor);

        if (!includeAllPorts)
        {
            var preferredOnly = list.Where(static item => item.IsPreferredDevice).ToArray();
            if (preferredOnly.Length > 0)
            {
                return Task.FromResult<IReadOnlyList<SerialPortDescriptor>>(preferredOnly);
            }
        }

        return Task.FromResult<IReadOnlyList<SerialPortDescriptor>>(list);
    }

    private static SerialPortDescriptor BuildDescriptor(string portName, PortMetadata? metadata)
    {
        var pnpId = metadata?.PnpDeviceId;
        var vidPid = ExtractVidPid(pnpId);
        var rank = ResolveRank(vidPid, metadata?.FriendlyName);

        return new SerialPortDescriptor
        {
            PortName = portName,
            DisplayName = metadata?.FriendlyName ?? portName,
            PnpDeviceId = pnpId,
            VidPid = vidPid,
            IsPreferredDevice = rank < 100,
            PriorityRank = rank,
        };
    }

    private static Dictionary<string, PortMetadata> TryLoadMetadataByPort()
    {
        var map = new Dictionary<string, PortMetadata>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

            foreach (var item in searcher.Get())
            {
                using var managementObject = item as ManagementObject;
                if (managementObject is null)
                {
                    continue;
                }

                var friendlyName = managementObject["Name"] as string;
                var pnpDeviceId = managementObject["PNPDeviceID"] as string;
                if (string.IsNullOrWhiteSpace(friendlyName))
                {
                    continue;
                }

                var portName = ExtractPortNameFromFriendlyName(friendlyName);
                if (string.IsNullOrWhiteSpace(portName))
                {
                    continue;
                }

                map[portName] = new PortMetadata
                {
                    FriendlyName = friendlyName,
                    PnpDeviceId = pnpDeviceId,
                };
            }
        }
        catch
        {
            // WMI can fail in restricted environments; fallback to port names only.
        }

        return map;
    }

    private static string? ExtractPortNameFromFriendlyName(string friendlyName)
    {
        var match = ComPortRegex.Match(friendlyName);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[1].Value.ToUpperInvariant();
    }

    private static string? ExtractVidPid(string? pnpDeviceId)
    {
        if (string.IsNullOrWhiteSpace(pnpDeviceId))
        {
            return null;
        }

        var match = VidPidRegex.Match(pnpDeviceId);
        if (!match.Success)
        {
            return null;
        }

        var vid = match.Groups[1].Value.ToUpperInvariant();
        var pid = match.Groups[2].Value.ToUpperInvariant();
        return $"{vid}:{pid}";
    }

    private static int ResolveRank(string? vidPid, string? friendlyName)
    {
        if (!string.IsNullOrWhiteSpace(vidPid))
        {
            var vid = vidPid.Split(':')[0];
            var preferredIndex = Array.FindIndex(PreferredVids, candidate => string.Equals(candidate, vid, StringComparison.OrdinalIgnoreCase));
            if (preferredIndex >= 0)
            {
                return preferredIndex;
            }
        }

        if (!string.IsNullOrWhiteSpace(friendlyName)
            && friendlyName.Contains("ESP", StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        return 100;
    }

    private static int CompareDescriptor(SerialPortDescriptor left, SerialPortDescriptor right)
    {
        var rankComparison = left.PriorityRank.CompareTo(right.PriorityRank);
        if (rankComparison != 0)
        {
            return rankComparison;
        }

        var leftPortNumber = TryExtractPortNumber(left.PortName);
        var rightPortNumber = TryExtractPortNumber(right.PortName);
        if (leftPortNumber.HasValue && rightPortNumber.HasValue && leftPortNumber.Value != rightPortNumber.Value)
        {
            return leftPortNumber.Value.CompareTo(rightPortNumber.Value);
        }

        return string.Compare(left.PortName, right.PortName, StringComparison.OrdinalIgnoreCase);
    }

    private static int? TryExtractPortNumber(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return null;
        }

        var match = PortNumberRegex.Match(portName);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var value)
            ? value
            : null;
    }

    private sealed class PortMetadata
    {
        public string? FriendlyName { get; init; }

        public string? PnpDeviceId { get; init; }
    }
}
