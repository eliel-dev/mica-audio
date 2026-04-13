namespace Device.Protocol.Models;

public enum DeviceCommandType
{
    EnterProvisioning = 1,
    RevokeAndRestart = 2,
    TestLed = 3,
    InstallApp = 5,
    ActivateApp = 6,
    SetAppConfig = 7,
    SetBrightness = 8,
    UpdateFirmware = 9,
    QueuePanelsBatch = 10,
}
