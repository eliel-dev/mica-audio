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
    public void VisualizerEngine_ShouldExposeOnlySupported2dRenderers()
    {
        var engine = new VisualizerEngine();
        var rendererIds = engine.Renderers.Select(static renderer => renderer.RendererId).ToArray();

        Assert.Contains(RendererIds.VizzyBlobNeon, rendererIds);
        Assert.Contains(RendererIds.VizzyOrbitRings, rendererIds);
        Assert.Contains(RendererIds.PolarArcs, rendererIds);
        Assert.DoesNotContain("vizzy-hyper-tunnel", rendererIds);
        Assert.DoesNotContain("vizzy-hyper-tunnel-shader", rendererIds);
    }

    [Fact]
    public void DefaultPresets_ShouldContainEnabled2dPresets_AndNoHyperTunnel()
    {
        var presets = GetDefaultPresets();
        var presetIds = presets.Select(static preset => preset.PresetId).ToArray();

        Assert.Contains("spectrum-vizzy-blob-neon", presetIds);
        Assert.Contains("spectrum-vizzy-orbit-rings", presetIds);
        Assert.Contains("spectrum-polar-arcs", presetIds);
        Assert.DoesNotContain("spectrum-vizzy-hyper-tunnel", presetIds);
        Assert.DoesNotContain("spectrum-vizzy-hyper-tunnel-shader", presetIds);
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

        var retiredClassic = new PresetDefinition
        {
            SchemaVersion = 1,
            PresetId = "spectrum-vizzy-hyper-tunnel",
            Name = "Hyper Tunnel Classic",
            RendererId = "vizzy-hyper-tunnel",
        };

        var retiredShader = new PresetDefinition
        {
            SchemaVersion = 1,
            PresetId = "spectrum-vizzy-hyper-tunnel-shader",
            Name = "Hyper Tunnel",
            RendererId = "vizzy-hyper-tunnel-shader",
        };

        try
        {
            await File.WriteAllTextAsync(Path.Combine(presetsDir, "spectrum-bars.json"), JsonSerializer.Serialize(outdatedDefault)).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(presetsDir, "custom-user-preset.json"), JsonSerializer.Serialize(customUserPreset)).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(presetsDir, "spectrum-vizzy-hyper-tunnel.json"), JsonSerializer.Serialize(retiredClassic)).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(presetsDir, "spectrum-vizzy-hyper-tunnel-shader.json"), JsonSerializer.Serialize(retiredShader)).ConfigureAwait(false);

            var loaded = await LoadPresetsThroughRepositoryAsync(appDataRoot).ConfigureAwait(false);

            Assert.Contains(loaded, static preset => string.Equals(preset.PresetId, "spectrum-vizzy-blob-neon", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded, static preset => string.Equals(preset.PresetId, "spectrum-vizzy-orbit-rings", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded, static preset => string.Equals(preset.PresetId, "spectrum-polar-arcs", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(loaded, static preset => string.Equals(preset.PresetId, "spectrum-vizzy-hyper-tunnel", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(loaded, static preset => string.Equals(preset.PresetId, "spectrum-vizzy-hyper-tunnel-shader", StringComparison.OrdinalIgnoreCase));

            var custom = loaded.Single(static preset => string.Equals(preset.PresetId, "custom-user-preset", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Preset customizado", custom.Name);
            Assert.Equal(RendererIds.Line, custom.RendererId);

            var bars = loaded.Single(static preset => string.Equals(preset.PresetId, "spectrum-bars", StringComparison.OrdinalIgnoreCase));
            Assert.True(bars.SchemaVersion > outdatedDefault.SchemaVersion);

            Assert.False(File.Exists(Path.Combine(presetsDir, "spectrum-vizzy-hyper-tunnel.json")));
            Assert.False(File.Exists(Path.Combine(presetsDir, "spectrum-vizzy-hyper-tunnel-shader.json")));
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
    public async Task PresetRepository_LoadOrSeedAsync_ShouldMigrateRetiredHyperTunnelRenderers_ToAudioMotionClone()
    {
        var appDataRoot = Path.Combine(Path.GetTempPath(), "mica-audio-smoke", Guid.NewGuid().ToString("N"));
        var presetsDir = Path.Combine(appDataRoot, "presets");
        Directory.CreateDirectory(presetsDir);

        var classicCustom = new PresetDefinition
        {
            SchemaVersion = 4,
            PresetId = "custom-hyper-classic",
            Name = "Meu tunnel classico",
            RendererId = "vizzy-hyper-tunnel",
            RendererParameters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["tunnelSpeed"] = 1.4f,
                ["tunnelWarp"] = 0.2f,
                ["lineThickness"] = 5f,
            },
        };

        var shaderCustom = new PresetDefinition
        {
            SchemaVersion = 5,
            PresetId = "custom-hyper-shader",
            Name = "Meu tunnel shader",
            RendererId = "vizzy-hyper-tunnel-shader",
            RendererParameters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["tunnelSliceCount"] = 72f,
                ["tunnelFogAmount"] = 0.3f,
                ["heightScale"] = 0.91f,
            },
        };

        try
        {
            await File.WriteAllTextAsync(Path.Combine(presetsDir, "custom-hyper-classic.json"), JsonSerializer.Serialize(classicCustom)).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(presetsDir, "custom-hyper-shader.json"), JsonSerializer.Serialize(shaderCustom)).ConfigureAwait(false);

            var loaded = await LoadPresetsThroughRepositoryAsync(appDataRoot).ConfigureAwait(false);

            var migratedClassic = loaded.Single(static preset => string.Equals(preset.PresetId, "custom-hyper-classic", StringComparison.OrdinalIgnoreCase));
            var migratedShader = loaded.Single(static preset => string.Equals(preset.PresetId, "custom-hyper-shader", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(RendererIds.AudioMotionClone, migratedClassic.RendererId);
            Assert.Equal(RendererIds.AudioMotionClone, migratedShader.RendererId);
            Assert.DoesNotContain("tunnelSpeed", migratedClassic.RendererParameters.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("tunnelSliceCount", migratedShader.RendererParameters.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("lineThickness", migratedClassic.RendererParameters.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("heightScale", migratedShader.RendererParameters.Keys, StringComparer.OrdinalIgnoreCase);
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
    public void PolarArcsRenderer_Render_ShouldNotThrow_ForValidAndEmptyFrames()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CanvasRenderTarget? target = null;
        CanvasDrawingSession? drawingSession = null;

        try
        {
            var renderer = new PolarArcsRenderer();
            var preset = CreateTestPreset(RendererIds.PolarArcs);
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
            return;
        }
        finally
        {
            drawingSession?.Dispose();
            target?.Dispose();
        }
    }

    [Fact]
    public void AudioMotionCloneRenderer_Render_ShouldNotThrow_ForValidAndEmptyFrames()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CanvasRenderTarget? target = null;
        CanvasDrawingSession? drawingSession = null;

        try
        {
            var renderer = new AudioMotionCloneRenderer();
            var preset = CreateTestPreset(RendererIds.AudioMotionClone);
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
            return;
        }
        finally
        {
            drawingSession?.Dispose();
            target?.Dispose();
        }
    }

    [Fact]
    public void AppAssembly_ShouldNotExposeRemovedPresetGalleryPreviewTypes()
    {
        var appAssembly = typeof(App.WinUI.App).Assembly;

        Assert.Null(appAssembly.GetType("App.WinUI.Views.Controls.PresetPreviewThumbnailControl", throwOnError: false));
        Assert.Null(appAssembly.GetType("App.WinUI.Views.Controls.PresetGalleryCardControl", throwOnError: false));
        Assert.Null(appAssembly.GetType("App.WinUI.Services.Visualizer.PresetPreviewSignalFactory", throwOnError: false));
        Assert.Null(appAssembly.GetType("App.WinUI.Services.Visualizer.PresetPreviewSettingsSnapshot", throwOnError: false));
        Assert.NotNull(appAssembly.GetType("App.WinUI.Services.Visualizer.VisualizerAnalyzerConfigFactory", throwOnError: false));
        Assert.NotNull(appAssembly.GetType("App.WinUI.Services.Visualizer.PresetNavigationHelper", throwOnError: false));
        Assert.Null(appAssembly.GetType("App.WinUI.Services.Visualizer.PresetGalleryEntry", throwOnError: false));
        Assert.Null(appAssembly.GetType("App.WinUI.Services.Visualizer.PresetGalleryGroupingService", throwOnError: false));
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
                    new PaletteStop { Offset = 0f, Color = new RgbaColor(255, 255, 255) },
                    new PaletteStop { Offset = 1f, Color = new RgbaColor(255, 255, 255) },
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






