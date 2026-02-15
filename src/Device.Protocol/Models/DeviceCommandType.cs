namespace Device.Protocol.Models;

public enum DeviceCommandType
{
    EnterProvisioning = 1,
    RevokeAndRestart = 2,
    TestLed = 3,
    StartOta = 4,
}
