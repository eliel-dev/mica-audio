namespace App.Headless.Services;

/// <summary>
/// Wraps HeadlessAudioPipeline as an IHostedService.
/// </summary>
internal sealed class AudioPipelineHostedService : IHostedService
{
    private readonly HeadlessAudioPipeline pipeline;
    private readonly ILogger<AudioPipelineHostedService> logger;

    public AudioPipelineHostedService(HeadlessAudioPipeline pipeline, ILogger<AudioPipelineHostedService> logger)
    {
        this.pipeline = pipeline;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        pipeline.StatusChanged += (_, msg) => logger.LogInformation("{Status}", msg);

        try
        {
            await pipeline.StartAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Audio pipeline iniciado.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nao foi possivel iniciar audio pipeline. Continuando sem captura.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await pipeline.StopAsync().ConfigureAwait(false);
        logger.LogInformation("Audio pipeline parado.");
    }
}
