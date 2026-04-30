namespace Device.Protocol.Models;

public enum DeviceControlPlaneState
{
    Offline = 0,
    LegacyOnly = 1,
    MqttOnline = 2,
}
