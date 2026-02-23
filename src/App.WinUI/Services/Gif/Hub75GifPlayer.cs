using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Gif;

// DOCS: docs/wiki/guides/load-gif-hub75.md#player-12-fps
internal sealed class Hub75GifPlayer : IDisposable
{
    private static readonly IReadOnlyList<RgbaColor[]> EmptyFrames = Array.Empty<RgbaColor[]>();

    private readonly object gate = new();
    private readonly TimeSpan frameInterval;
    private Timer? timer;
    private IReadOnlyList<RgbaColor[]> frames = EmptyFrames;
    private int frameIndex;
    private bool isPlaying;

    public Hub75GifPlayer(TimeSpan frameInterval)
    {
        this.frameInterval = frameInterval > TimeSpan.Zero
            ? frameInterval
            : throw new ArgumentOutOfRangeException(nameof(frameInterval));
    }

    public bool IsPlaying
    {
        get
        {
            lock (gate)
            {
                return isPlaying;
            }
        }
    }

    public int FrameCount
    {
        get
        {
            lock (gate)
            {
                return frames.Count;
            }
        }
    }

    public event EventHandler<RgbaColor[]>? FrameReady;

    public void SetFrames(IReadOnlyList<RgbaColor[]> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        lock (gate)
        {
            this.frames = frames.Count == 0 ? EmptyFrames : frames;
            frameIndex = 0;
        }
    }

    public bool Play()
    {
        RgbaColor[]? firstFrame = null;
        lock (gate)
        {
            if (frames.Count == 0)
            {
                return false;
            }

            if (timer is null)
            {
                timer = new Timer(OnTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            if (isPlaying)
            {
                return true;
            }

            isPlaying = true;
            firstFrame = frames[frameIndex];
            frameIndex = (frameIndex + 1) % frames.Count;
            timer.Change(frameInterval, frameInterval);
        }

        FrameReady?.Invoke(this, firstFrame);
        return true;
    }

    public void Pause()
    {
        lock (gate)
        {
            if (!isPlaying)
            {
                return;
            }

            isPlaying = false;
            timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            isPlaying = false;
            frameIndex = 0;
            timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            isPlaying = false;
            timer?.Dispose();
            timer = null;
            frames = EmptyFrames;
            frameIndex = 0;
        }
    }

    private void OnTick(object? _)
    {
        RgbaColor[]? frame = null;

        lock (gate)
        {
            if (!isPlaying || frames.Count == 0)
            {
                return;
            }

            frame = frames[frameIndex];
            frameIndex = (frameIndex + 1) % frames.Count;
        }

        FrameReady?.Invoke(this, frame);
    }
}
