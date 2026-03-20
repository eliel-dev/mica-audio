namespace MicaAudio.Core.Config;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#pontos-de-alteracao-frequente
public class MicaAudioOptions
{
    public const string Position = "MicaAudio";

    public string AppDataRoot { get; set; } = string.Empty;

    public string DevicesFilePath { get; set; } = string.Empty;

    public string SettingsFilePath { get; set; } = string.Empty;

    public string PresetsDirectory { get; set; } = string.Empty;

    public string AppsCatalogPath { get; set; } = string.Empty;

    public string AppsModifierStatePath { get; set; } = string.Empty;

    public string PanelsFilePath { get; set; } = string.Empty;

    public string CrashLogPath { get; set; } = string.Empty;

    public string PrecompiledFirmwareDirectory { get; set; } = string.Empty;

    public string WorkspaceRoot { get; set; } = string.Empty;
}
