namespace Audio.Loopback.Capture;

public enum CaptureStatus
{
    Idle = 0,
    Starting = 1,
    Running = 2,
    Recovering = 3,
    Error = 4,
    Stopped = 5,
}
