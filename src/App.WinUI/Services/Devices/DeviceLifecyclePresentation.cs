// DOCS: docs/wiki/reference/code-index.md#estado-de-device
namespace App.WinUI.Services.Devices;

internal readonly record struct DeviceLifecyclePresentation(
    string PrimaryStateLabel,
    string SecondaryStateLabel,
    string DetailLabel,
    string LastSeenLabel,
    bool CanRunCommands,
    bool IsNeverSeen,
    bool IsConfigUncertain,
    DeviceLifecycleIcon Icon,
    DeviceLifecycleTone Tone);

