namespace Audio.Loopback.Capture;

public sealed class CaptureStatusChangedEventArgs : EventArgs
{
    public CaptureStatusChangedEventArgs(CaptureStatus status, string message, Exception? exception = null)
    {
        Status = status;
        Message = message;
        Exception = exception;
    }

    public CaptureStatus Status { get; }

    public string Message { get; }

    public Exception? Exception { get; }
}
