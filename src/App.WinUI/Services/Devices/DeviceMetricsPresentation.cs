// DOCS: docs/wiki/modules/app-winui.md#modulo-appwinui
namespace App.WinUI.Services.Devices;

internal readonly record struct DeviceMetricsPresentation(
    string StatusLabel,
    string UptimeLabel,
    string HeapLabel,
    string PsramLabel,
    string NetworkLabel,
    int? LoopLoadPercent,
    double LoopLoadProgress,
    double? HeapFragmentationProgress,
    double? PsramFragmentationProgress,
    bool HasMetrics,
    bool IsOfflineSnapshot,
    bool IsPsramAvailable,
    string PlaceholderMessage);
