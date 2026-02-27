using System.Diagnostics;
using System.Numerics;
using ComputeSharp;
using ComputeSharp.D2D1.WinUI;
using Microsoft.Graphics.Canvas;
using Visual.Win2D.Engine;
using Visual.Win2D.Shaders;
using Windows.Foundation;
using Windows.UI;

namespace Visual.Win2D.Renderers;

// DOCS: docs/wiki/modules/visual-win2d.md#hyper-tunnel-shader-gpu
public sealed class VizzyHyperTunnelShaderRenderer : IRenderer
{
    private readonly VizzyHyperTunnelRenderer classicFallback = new();

    private PixelShaderEffect<HyperTunnelShadertoyShader>? shaderEffect;
    private CanvasRenderTarget? shaderSurface;
    private CanvasRenderTarget? blankInputSurface;
    private int cachedSurfaceWidth;
    private int cachedSurfaceHeight;
    private bool shaderUnavailable;
    private bool fallbackWarningLogged;

    private float animationTime;
    private float audioActivityTime;

    private float smoothedFrameMs = 16.7f;
    private float qualityCooldownSeconds;
    private int qualityTier = 2;

    public string RendererId => RendererIds.VizzyHyperTunnelShader;

    public string DisplayName => "Hyper Tunnel";

    public void Render(RenderContext context)
    {
        var bands = context.Frame.BandsDisplay;
        if (context.Width <= 1f || context.Height <= 1f || bands.Length == 0)
        {
            return;
        }

        var audio = HyperTunnelAudioMapper.Map(context.Frame);
        UpdateAudioTimers(context.DeltaSeconds, audio);
        UpdateAdaptiveQuality(context.DeltaSeconds);

        if (shaderUnavailable)
        {
            classicFallback.Render(context);
            return;
        }

        var quality = ResolveQuality();
        var scaledWidth = Math.Max(2, (int)MathF.Round(context.Width * quality.Scale));
        var scaledHeight = Math.Max(2, (int)MathF.Round(context.Height * quality.Scale));

        var speed = Math.Clamp(context.Param("tunnelSpeed", 1.10f), 0.25f, 3.40f);
        animationTime += context.DeltaSeconds * speed * (0.75f + (audio.Bass * 0.85f) + (audio.Level * 0.30f));

        try
        {
            EnsureShaderResources(context.DrawingSession.Device, scaledWidth, scaledHeight);

            var warm = ToVector3(context.Palette.ColorAt(0.25f));
            var cool = ToVector3(context.Palette.ColorAt(0.78f));

            var uniforms = new HyperTunnelShaderUniforms(
                Time: animationTime,
                AudioActivityTime: audioActivityTime,
                Bass: audio.Bass,
                Mid: audio.Mid,
                High: audio.High,
                Level: audio.Level,
                Band32: audio.Band32,
                Band128: audio.Band128,
                TunnelBaseRadius: Math.Clamp(context.Param("tunnelBaseRadius", 0.22f), 0.10f, 0.42f),
                TunnelDepth: Math.Clamp(context.Param("tunnelDepth", 1.00f), 0.55f, 1.80f),
                TunnelSpeed: speed,
                TunnelWarp: Math.Clamp(context.Param("tunnelWarp", 0.14f), 0.01f, 0.42f),
                TunnelFogAmount: Math.Clamp(context.Param("tunnelFogAmount", 0.18f), 0f, 0.65f),
                WarmColor: warm,
                CoolColor: cool);

            var shader = new HyperTunnelShadertoyShader(
                resolution: new float2(scaledWidth, scaledHeight),
                time: uniforms.Time,
                audioActivityTime: uniforms.AudioActivityTime,
                bass: uniforms.Bass,
                mid: uniforms.Mid,
                high: uniforms.High,
                level: uniforms.Level,
                band32: uniforms.Band32,
                band128: uniforms.Band128,
                tunnelBaseRadius: uniforms.TunnelBaseRadius,
                tunnelDepth: uniforms.TunnelDepth,
                tunnelSpeed: uniforms.TunnelSpeed,
                tunnelWarp: uniforms.TunnelWarp,
                tunnelFogAmount: uniforms.TunnelFogAmount,
                traceIterations: quality.TraceIterations,
                steamSteps: quality.SteamSteps,
                warmColor: new float3(uniforms.WarmColor.X, uniforms.WarmColor.Y, uniforms.WarmColor.Z),
                coolColor: new float3(uniforms.CoolColor.X, uniforms.CoolColor.Y, uniforms.CoolColor.Z));

            shaderEffect ??= new PixelShaderEffect<HyperTunnelShadertoyShader>();
            shaderEffect.CacheOutput = false;
            shaderEffect.BufferPrecision = CanvasBufferPrecision.Precision8UIntNormalized;
            shaderEffect.Sources[0] = blankInputSurface!;
            shaderEffect.ConstantBuffer = shader;

            using (var shaderDrawingSession = shaderSurface!.CreateDrawingSession())
            {
                shaderDrawingSession.Clear(Color.FromArgb(255, 0, 0, 0));
                shaderDrawingSession.DrawImage(shaderEffect);
            }

            context.DrawingSession.DrawImage(
                shaderSurface,
                new Rect(0, 0, context.Width, context.Height),
                new Rect(0, 0, cachedSurfaceWidth, cachedSurfaceHeight));
        }
        catch (Exception ex) when (IsShaderRecoverableFailure(ex))
        {
            shaderUnavailable = true;
            DisposeShaderResources();

            if (!fallbackWarningLogged)
            {
                fallbackWarningLogged = true;
                Trace.TraceWarning($"Hyper Tunnel shader indisponivel. Fallback para classic. Erro: {ex.GetType().Name}: {ex.Message}");
            }

            classicFallback.Render(context);
        }
    }

    private void EnsureShaderResources(CanvasDevice device, int width, int height)
    {
        if (blankInputSurface is not null && shaderSurface is not null && cachedSurfaceWidth == width && cachedSurfaceHeight == height)
        {
            return;
        }

        DisposeShaderResources();

        blankInputSurface = new CanvasRenderTarget(device, width, height, 96);
        using (var inputDrawingSession = blankInputSurface.CreateDrawingSession())
        {
            inputDrawingSession.Clear(Color.FromArgb(255, 0, 0, 0));
        }

        shaderSurface = new CanvasRenderTarget(device, width, height, 96);
        cachedSurfaceWidth = width;
        cachedSurfaceHeight = height;
    }

    private void DisposeShaderResources()
    {
        shaderEffect = null;

        if (shaderSurface is not null)
        {
            shaderSurface.Dispose();
            shaderSurface = null;
        }

        if (blankInputSurface is not null)
        {
            blankInputSurface.Dispose();
            blankInputSurface = null;
        }

        cachedSurfaceWidth = 0;
        cachedSurfaceHeight = 0;
    }

    private void UpdateAudioTimers(float deltaSeconds, HyperTunnelAudioState audio)
    {
        var activity = (audio.Level > 0.02f) || ((audio.Bass + audio.Mid + audio.High) > 0.08f);

        if (activity)
        {
            audioActivityTime = Math.Min(1f, audioActivityTime + (deltaSeconds * 0.9f));
        }
        else
        {
            audioActivityTime = Math.Max(0f, audioActivityTime - (deltaSeconds * 0.35f));
        }
    }

    private void UpdateAdaptiveQuality(float deltaSeconds)
    {
        var frameMs = Math.Clamp(deltaSeconds * 1000f, 8f, 80f);
        smoothedFrameMs = Lerp(smoothedFrameMs, frameMs, 0.08f);

        qualityCooldownSeconds = MathF.Max(0f, qualityCooldownSeconds - deltaSeconds);
        if (qualityCooldownSeconds > 0f)
        {
            return;
        }

        if (smoothedFrameMs > 20f && qualityTier > 0)
        {
            qualityTier--;
            qualityCooldownSeconds = 0.35f;
            return;
        }

        if (smoothedFrameMs < 15f && qualityTier < 2)
        {
            qualityTier++;
            qualityCooldownSeconds = 0.65f;
        }
    }

    private QualityConfig ResolveQuality()
    {
        return qualityTier switch
        {
            2 => new QualityConfig(Scale: 1.0f, TraceIterations: 100, SteamSteps: 24),
            1 => new QualityConfig(Scale: 0.75f, TraceIterations: 72, SteamSteps: 16),
            _ => new QualityConfig(Scale: 0.5f, TraceIterations: 56, SteamSteps: 10),
        };
    }

    private static Vector3 ToVector3(Color color)
    {
        const float inv = 1f / 255f;
        return new Vector3(color.R * inv, color.G * inv, color.B * inv);
    }

    private static bool IsShaderRecoverableFailure(Exception exception)
    {
        return exception is InvalidOperationException
            or ArgumentException
            or ObjectDisposedException
            or System.Runtime.InteropServices.COMException;
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + ((to - from) * t);
    }

    private readonly record struct QualityConfig(float Scale, int TraceIterations, int SteamSteps);
}
