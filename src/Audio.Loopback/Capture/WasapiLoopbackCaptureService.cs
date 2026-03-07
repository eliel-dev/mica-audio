using System.Diagnostics;
using System.Threading.Channels;
using MicaAudio.Core.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Audio.Loopback.Capture;

// DOCS: docs/wiki/modules/audio-loopback.md#modulo-audioloopback
public sealed class WasapiLoopbackCaptureService : ILoopbackCapture
{
    private readonly object gate = new();

    private Channel<PcmFrame> channel = Channel.CreateBounded<PcmFrame>(new BoundedChannelOptions(8)
    {
        SingleReader = false,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.DropOldest,
    });

    private readonly MMDeviceEnumerator deviceEnumerator = new();
    private CaptureConfig config = new();
    private LowLatencyWasapiLoopbackCapture? capture;
    private Timer? defaultDeviceMonitor;
    private string? activeDeviceId;

    private bool isStarted;
    private bool isDisposed;
    private bool manualStop;
    private bool restarting;

    public ChannelReader<PcmFrame> Frames => channel.Reader;

    public event EventHandler<CaptureStatusChangedEventArgs>? StatusChanged;

    public Task StartAsync(CaptureConfig config, CancellationToken cancellationToken = default)
    {
        // DOCS: docs/wiki/modules/audio-loopback.md#fluxo-de-execucao
        lock (gate)
        {
            ThrowIfDisposed();
            if (isStarted)
            {
                return Task.CompletedTask;
            }

            this.config = config;
            channel = Channel.CreateBounded<PcmFrame>(new BoundedChannelOptions(Math.Max(2, config.ChannelCapacity))
            {
                SingleReader = false,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest,
            });

            RaiseStatus(CaptureStatus.Starting, "Inicializando captura loopback...");

            StartCaptureUnsafe();
            StartDefaultDeviceMonitorUnsafe();
            isStarted = true;
            manualStop = false;

            RaiseStatus(CaptureStatus.Running, "Captura loopback em execucao.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (gate)
        {
            if (!isStarted)
            {
                return Task.CompletedTask;
            }

            manualStop = true;
            StopDefaultDeviceMonitorUnsafe();
            StopCaptureUnsafe();

            isStarted = false;
            channel.Writer.TryComplete();
            RaiseStatus(CaptureStatus.Stopped, "Captura loopback parada.");
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        deviceEnumerator.Dispose();
        isDisposed = true;
    }

    private async Task RestartCaptureAsync(string reason)
    {
        lock (gate)
        {
            if (!isStarted || restarting || isDisposed)
            {
                return;
            }

            restarting = true;
            RaiseStatus(CaptureStatus.Recovering, $"Recuperando captura de audio ({reason})...");
        }

        try
        {
            await Task.Delay(300).ConfigureAwait(false);

            lock (gate)
            {
                StopCaptureUnsafe();
                StartCaptureUnsafe();
                RaiseStatus(CaptureStatus.Running, "Captura loopback recuperada.");
            }
        }
        catch (Exception ex)
        {
            lock (gate)
            {
                RaiseStatus(CaptureStatus.Error, "Nao foi possivel recuperar a captura loopback. Tente novamente reiniciando o aplicativo.", ex);
            }
        }
        finally
        {
            lock (gate)
            {
                restarting = false;
            }
        }
    }

    private void StartCaptureUnsafe()
    {
        var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        activeDeviceId = device.ID;

        var latencyMs = global::System.Math.Clamp(config.BufferMilliseconds, 8, 20);

        capture = new LowLatencyWasapiLoopbackCapture(device, latencyMs)
        {
            ShareMode = AudioClientShareMode.Shared,
        };
        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += OnRecordingStopped;
        capture.StartRecording();
    }

    private void StopCaptureUnsafe()
    {
        if (capture is null)
        {
            return;
        }

        capture.DataAvailable -= OnDataAvailable;
        capture.RecordingStopped -= OnRecordingStopped;

        try
        {
            capture.StopRecording();
        }
        catch
        {
            // Endpoint state can change between callback and stop.
        }

        capture.Dispose();
        capture = null;
        activeDeviceId = null;
    }

    private void StartDefaultDeviceMonitorUnsafe()
    {
        defaultDeviceMonitor ??= new Timer(
            static state => _ = ((WasapiLoopbackCaptureService)state!).HandleDefaultDeviceCheckAsync(),
            this,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2));
    }

    private void StopDefaultDeviceMonitorUnsafe()
    {
        defaultDeviceMonitor?.Dispose();
        defaultDeviceMonitor = null;
    }

    private Task HandleDefaultDeviceCheckAsync()
    {
        lock (gate)
        {
            if (!isStarted || isDisposed || restarting)
            {
                return Task.CompletedTask;
            }
        }

        try
        {
            var defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (!string.Equals(defaultDevice.ID, activeDeviceId, StringComparison.Ordinal))
            {
                return RestartCaptureAsync("dispositivo de saida alterado");
            }
        }
        catch (Exception ex)
        {
            RaiseStatus(CaptureStatus.Error, "Nenhum dispositivo de saida ativo para captura loopback.", ex);
        }

        return Task.CompletedTask;
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (manualStop || isDisposed)
        {
            return;
        }

        if (e.Exception is not null)
        {
            RaiseStatus(CaptureStatus.Error, "Captura de audio interrompida. Tentando recuperar...", e.Exception);
        }

        _ = RestartCaptureAsync("fluxo interrompido");
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var currentCapture = capture;
        if (currentCapture is null || e.BytesRecorded <= 0)
        {
            return;
        }

        try
        {
            var sourceSamples = PcmConversion.DecodeToFloat(e.Buffer, e.BytesRecorded, currentCapture.WaveFormat);
            if (sourceSamples.Length == 0)
            {
                return;
            }

            var mono = PcmConversion.DownmixToMono(sourceSamples, currentCapture.WaveFormat.Channels);
            var mono48k = PcmConversion.ResampleLinear(mono, currentCapture.WaveFormat.SampleRate, config.TargetSampleRate);
            if (mono48k.Length == 0)
            {
                return;
            }

            var frame = new PcmFrame(mono48k, Stopwatch.GetTimestamp());
            channel.Writer.TryWrite(frame);
        }
        catch (Exception ex)
        {
            RaiseStatus(CaptureStatus.Error, "Erro ao processar o buffer de loopback.", ex);
        }
    }

    private void RaiseStatus(CaptureStatus status, string message, Exception? exception = null)
    {
        StatusChanged?.Invoke(this, new CaptureStatusChangedEventArgs(status, message, exception));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
    }
}


