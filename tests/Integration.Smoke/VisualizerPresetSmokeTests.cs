using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using App.WinUI;
using Microsoft.Extensions.Options;
using Microsoft.Graphics.Canvas;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;
using Visual.Win2D.Renderers;

namespace Integration.Smoke;

public sealed class VisualizerPresetSmokeTests
{
    [Fact]
    public void VisualizerEngine_ShouldExposeVizzyRenderers()
    {
        var engine = new VisualizerEngine();
        var rendererIds = engine.Renderers.Select(static renderer => renderer.RendererId).ToArray();

        Assert.Contains(RendererIds.VizzyBlobNeon, rendererIds);
        Assert.Contains(RendererIds.VizzyOrbitRings, rendererIds);
        Assert.Contains(RendererIds.VizzyHyperTunnel, rendererIds);
    }

    [Fact]
    public void DefaultPresets_ShouldContainVizzyPresets()
    {
        var presets = GetDefaultPresets();
        var presetIds = presets.Select(static preset => preset.PresetId).ToArray();

        Assert.Contains("spectrum-vizzy-blob-neon", presetIds);
        Assert.Contains("spectrum-vizzy-orbit-rings", presetIds);
        Assert.Contains("spectrum-vizzy-hyper-tunnel", presetIds);
    }

    [Fact]
    public async Task PresetRepository_LoadOrSeedAsync_ShouldMergeDefaultsWithoutRemovingCustomPresets()
    {
        var appDataRoot = Path.Combine(Path.GetTempPath(), "mica-audio-smoke", Guid.NewGuid().ToString("N"));
        var presetsDir = Path.Combine(appDataRoot, "presets");
        Directory.CreateDirectory(presetsDir);

        var outdatedDefault = new PresetDefinition
        {
            SchemaVersion = 1,
            PresetId = "spectrum-bars",
            Name = "Bars antigo",
            RendererId = RendererIds.Bars,
        };

        var customUserPreset = new PresetDefinition
        {
            SchemaVersion = 1,
            PresetId = "custom-user-preset",
            Name = "Preset customizado",
            RendererId = RendererIds.Line,
            DisplayBandCount = 42,
        };

        try
        {
            await File.WriteAllTextAsync(Path.Combine(presetsDir, "spectrum-bars.json"), JsonSerializer.Serialize(outdatedDefault)).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(presetsDir, "custom-user-preset.json"), JsonSerializer.Serialize(customUserPreset)).ConfigureAwait(false);

            var loaded = await LoadPresetsThroughRepositoryAsync(appDataRoot).ConfigureAwait(false);

            Assert.Contains(loaded, static preset => string.Equals(preset.PresetId, "spectrum-vizzy-blob-neon", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded, static preset => string.Equals(preset.PresetId, "spectrum-vizzy-orbit-rings", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded, static preset => string.Equals(preset.PresetId, "spectrum-vizzy-hyper-tunnel", StringComparison.OrdinalIgnoreCase));

            var custom = loaded.Single(static preset => string.Equals(preset.PresetId, "custom-user-preset", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Preset customizado", custom.Name);
            Assert.Equal(RendererIds.Line, custom.RendererId);

            var bars = loaded.Single(static preset => string.Equals(preset.PresetId, "spectrum-bars", StringComparison.OrdinalIgnoreCase));
            Assert.True(bars.SchemaVersion > outdatedDefault.SchemaVersion);
        }
        finally
        {
            if (Directory.Exists(appDataRoot))
            {
                Directory.Delete(appDataRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void VizzyHyperTunnelRenderer_Render_ShouldNotThrow_ForValidAndEmptyFrames()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CanvasRenderTarget? target = null;
        CanvasDrawingSession? drawingSession = null;

        try
        {
            var renderer = new VizzyHyperTunnelRenderer();
            var preset = CreateTestPreset(RendererIds.VizzyHyperTunnel);
            var peaks = new float[64];
            var bands = CreateBands(64);

            target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 320, 180, 96);
            drawingSession = target.CreateDrawingSession();

            renderer.Render(new RenderContext
            {
                DrawingSession = drawingSession,
                Preset = preset,
                Palette = new PaletteSampler(preset.Palette),
                Peaks = peaks,
                Frame = new MicaAudio.Core.Audio.SpectrumFrame(bands, bands, 0.55f, 0),
                Width = 320,
                Height = 180,
                DeltaSeconds = 1f / 60f,
            });

            renderer.Render(new RenderContext
            {
                DrawingSession = drawingSession,
                Preset = preset,
                Palette = new PaletteSampler(preset.Palette),
                Peaks = Array.Empty<float>(),
                Frame = new MicaAudio.Core.Audio.SpectrumFrame(Array.Empty<float>(), Array.Empty<float>(), 0f, 0),
                Width = 320,
                Height = 180,
                DeltaSeconds = 1f / 60f,
            });
        }
        catch (COMException)
        {
            // Some runners don't provide a compatible Win2D context. In this case, other smoke tests still validate wiring.
            return;
        }
        finally
        {
            drawingSession?.Dispose();
            target?.Dispose();
        }
    }

    private static IReadOnlyList<PresetDefinition> GetDefaultPresets()
    {
        var appAssembly = typeof(App.WinUI.App).Assembly;
        var defaultsType = appAssembly.GetType("App.WinUI.Services.DefaultPresets", throwOnError: true)!;
        var createMethod = defaultsType.GetMethod("Create", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = createMethod.Invoke(null, null)!;
        return (IReadOnlyList<PresetDefinition>)result;
    }

    private static async Task<IReadOnlyList<PresetDefinition>> LoadPresetsThroughRepositoryAsync(string appDataRoot)
    {
        var appAssembly = typeof(App.WinUI.App).Assembly;
        var repositoryType = appAssembly.GetType("App.WinUI.Services.PresetRepository", throwOnError: true)!;
        var options = Options.Create(new MicaAudioOptions
        {
            AppDataRoot = appDataRoot,
            PresetsDirectory = Path.Combine(appDataRoot, "presets"),
        });

        var repository = Activator.CreateInstance(repositoryType, options)!;
        var loadMethod = repositoryType.GetMethod("LoadOrSeedAsync", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

        var taskObject = loadMethod.Invoke(repository, new object?[] { CancellationToken.None })!;
        var task = (Task)taskObject;
        await task.ConfigureAwait(false);

        var resultProperty = taskObject.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)!;
        var result = resultProperty.GetValue(taskObject)!;

        return (IReadOnlyList<PresetDefinition>)result;
    }

    private static PresetDefinition CreateTestPreset(string rendererId)
    {
        return new PresetDefinition
        {
            PresetId = "smoke-render",
            Name = "Smoke Render",
            RendererId = rendererId,
            Palette = new GradientPalette
            {
                Name = "Smoke",
                Stops =
                [
                    new PaletteStop { Offset = 0f, Color = new RgbaColor(255, 120, 38) },
                    new PaletteStop { Offset = 1f, Color = new RgbaColor(0, 176, 220) },
                ],
            },
        };
    }

    private static float[] CreateBands(int count)
    {
        var bands = new float[count];
        for (var i = 0; i < count; i++)
        {
            var t = i / (float)Math.Max(1, count - 1);
            bands[i] = Math.Clamp((MathF.Sin(t * MathF.PI * 6f) * 0.25f) + 0.55f, 0f, 1f);
        }

        return bands;
    }
}
