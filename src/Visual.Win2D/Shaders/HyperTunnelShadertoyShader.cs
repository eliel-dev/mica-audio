using ComputeSharp;
using ComputeSharp.D2D1;

namespace Visual.Win2D.Shaders;

// DOCS: docs/wiki/modules/visual-win2d.md#hyper-tunnel-shader-gpu
[D2DInputCount(1)]
[D2DInputSimple(0)]
[D2DRequiresScenePosition]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
internal readonly partial struct HyperTunnelShadertoyShader(
    float2 resolution,
    float time,
    float audioActivityTime,
    float bass,
    float mid,
    float high,
    float level,
    float band32,
    float band128,
    float tunnelBaseRadius,
    float tunnelDepth,
    float tunnelSpeed,
    float tunnelWarp,
    float tunnelFogAmount,
    int traceIterations,
    int steamSteps,
    float3 warmColor,
    float3 coolColor) : ID2D1PixelShader
{
    private const float Pi = 3.14159265f;
    private const float TwoPi = Pi * 2f;
    private const float Fov = 70f;
    private const float Far = 1000f;
    private const float Infinity = 1e20f;
    private const int MaxIterations = 100;
    private const int MaxSteamSteps = 24;

    public float4 Execute()
    {
        float4 scenePosition = D2D.GetScenePosition();
        float2 fragCoord = new float2(scenePosition.X, scenePosition.Y);

        float2 safeResolution = Hlsl.Max(resolution, new float2(1f, 1f));
        float2 ouv = fragCoord / safeResolution;
        float2 uv = ouv - new float2(0.5f, 0.5f);

        uv *= Hlsl.Tan(Hlsl.Radians(Fov) * 0.5f) * 4f;
        uv.X *= safeResolution.X / safeResolution.Y;

        float t = time;
        float bassDrive = Hlsl.Saturate((bass * 0.9f) + (level * 0.25f));
        float midDrive = Hlsl.Saturate((mid * 0.95f) + (level * 0.20f));
        float highDrive = Hlsl.Saturate((high * 0.90f) + (band128 * 0.35f));

        float3 vuv = Hlsl.Normalize(new float3(Hlsl.Cos(t), Hlsl.Sin(t * 0.11f), Hlsl.Sin(t * 0.41f)));
        float3 ro = new float3(0f, 30f + (t * 100f), -0.1f);

        ro.X += YC(ro.Y * 0.1f) * 3f;
        ro.Z -= YC(ro.Y * 0.01f) * 4f;

        float3 vrp = new float3(0f, 50f + (t * 100f), 2f);
        vrp.X += YC(vrp.Y * 0.1f) * 3f;
        vrp.Z -= YC(vrp.Y * 0.01f) * 4f;

        float3 vpn = Hlsl.Normalize(vrp - ro);
        float3 u = Hlsl.Normalize(Hlsl.Cross(vuv, vpn));
        float3 v = Hlsl.Cross(vpn, u);
        float3 vcv = ro + vpn;

        float3 scrCoord = vcv + (uv.X * u * (safeResolution.X / safeResolution.Y)) + (uv.Y * v);
        float3 rd = Hlsl.Normalize(scrCoord - ro);


        float2 traceData = Trace(ro, rd);
        float hitDistance = traceData.X;
        float iterationFactor = traceData.Y;

        if (hitDistance >= Infinity * 0.5f)
        {
            return new float4(0f, 0f, 0f, 1f);
        }

        float3 hit = ro + (rd * hitDistance);

        float3 baseColor = Hlsl.Lerp(warmColor, coolColor, Hlsl.Saturate(0.5f + ((high - bass) * 0.45f)));
        float3 col = baseColor * FBm(new float3(hit.X, hit.Z, hit.Y) * 0.01f) * 20f;
        col.Z *= FBm(hit * 0.01f) * 10f;

        float3 sceneColor = iterationFactor * col + (col * 0.03f);
        sceneColor *= 1f + (0.9f * (Hlsl.Abs(FBm((hit * 0.002f) + new float3(3f, 3f, 3f)) * 10f) * FBm(new float3(0f, 0f, t * 0.1f) * 2f)));

        float audioGain = Hlsl.Lerp(0.70f, 1.45f, Hlsl.Saturate(audioActivityTime));
        float spectrumDrive = Hlsl.Saturate((bassDrive * 0.45f) + (midDrive * 0.35f) + (highDrive * 0.20f) + (level * 0.20f));
        float brightnessGain = Hlsl.Lerp(0.85f, 2.30f, highDrive);
        sceneColor *= audioGain
            * Hlsl.Lerp(0.80f, 1.65f, spectrumDrive)
            * Hlsl.Lerp(0.70f, 1.35f, bassDrive)
            * Hlsl.Lerp(0.45f, 1.15f, band128);

        float3 steamColor = Hlsl.Lerp(coolColor, warmColor, 0.20f) * Hlsl.Lerp(0.65f, 1.35f, band32);

        ro = hit;
        float distC = hitDistance;
        float steamFactor = 0f;
        int steamLimit = ClampInt(steamSteps, 6, MaxSteamSteps);

        for (int i = 0; i < MaxSteamSteps; i++)
        {
            if (i >= steamLimit)
            {
                break;
            }

            float3 rro = ro - (rd * distC);
            steamFactor += FBm(rro * 0.03f) * 0.1f;
            distC -= 3f;

            if (distC < 3f)
            {
                break;
            }
        }

        float fogGain = Hlsl.Lerp(0.85f, 1.45f, Hlsl.Saturate(bassDrive + (audioActivityTime * 0.35f)));
        sceneColor += steamColor * Hlsl.Pow(Hlsl.Abs(steamFactor * 1.5f), 3f) * (2.8f + (tunnelFogAmount * 5f)) * fogGain;

        float vignette = Hlsl.Saturate(1f - (Hlsl.Length(uv) * 0.5f));
        sceneColor *= vignette;

        float3 sourceColor = D2D.GetInput(0).RGB;
        sceneColor += sourceColor * 0.0001f;

        float distFactor = Hlsl.Max(hitDistance, 1f);
        float3 finalColor = Hlsl.Pow(Hlsl.Abs((sceneColor / distFactor) * 130f), new float3(0.8f, 0.8f, 0.8f)) * brightnessGain;

        return new float4(Hlsl.Saturate(finalColor), 1f);
    }

    private float Hash12(float2 p)
    {
        float h = Hlsl.Dot(p, new float2(127.1f, 311.7f));
        return Hlsl.Frac(Hlsl.Sin(h) * 43758.5453123f);
    }

    private float Noise3(float3 p)
    {
        float3 i = Hlsl.Floor(p);
        float3 f = Hlsl.Frac(p);
        float3 u = f * f * (new float3(3f, 3f, 3f) - (2f * f));

        float2 ii = new float2(i.X, i.Y) + (i.Z * new float2(5f, 5f));

        float a = Hash12(ii + new float2(0f, 0f));
        float b = Hash12(ii + new float2(1f, 0f));
        float c = Hash12(ii + new float2(0f, 1f));
        float d = Hash12(ii + new float2(1f, 1f));

        float v1 = Hlsl.Lerp(Hlsl.Lerp(a, b, u.X), Hlsl.Lerp(c, d, u.X), u.Y);

        ii += new float2(5f, 5f);

        a = Hash12(ii + new float2(0f, 0f));
        b = Hash12(ii + new float2(1f, 0f));
        c = Hash12(ii + new float2(0f, 1f));
        d = Hash12(ii + new float2(1f, 1f));

        float v2 = Hlsl.Lerp(Hlsl.Lerp(a, b, u.X), Hlsl.Lerp(c, d, u.X), u.Y);

        return Hlsl.Max(Hlsl.Lerp(v1, v2, u.Z), 0f);
    }

    private float FBm(float3 x)
    {
        float r = 0f;
        float w = 1f;
        float s = 1f;

        for (int i = 0; i < 4; i++)
        {
            w *= 0.25f;
            s *= 3f;
            r += w * Noise3(s * x);
        }

        return r;
    }

    private float YC(float x)
    {
        return (Hlsl.Cos(x * -0.134f) * Hlsl.Sin(x * 0.13f) * 15f) + FBm(new float3(x * 0.1f, 0f, 0f) * 55.4f);
    }

    private float FCylinderInf(float3 p, float r)
    {
        return Hlsl.Length(new float2(p.X, p.Z)) - r;
    }

    private float Map(float3 p)
    {
        float warpDrive = Hlsl.Saturate((mid * 0.95f) + (level * 0.20f));
        float bassDrive = Hlsl.Saturate((bass * 0.90f) + (level * 0.25f));
        float baseWarp = tunnelWarp * Hlsl.Lerp(0.85f, 1.15f, Hlsl.Saturate(tunnelSpeed / 3.4f));
        float warpAmount = baseWarp * (1f + (warpDrive * 3.2f)) + (band32 * 0.12f);

        p.X -= YC(p.Y * (0.1f + (warpAmount * 0.05f))) * (3f + (warpAmount * 6f));
        p.Z += YC((p.Y * (0.01f + (warpAmount * 0.015f))) + (time * (0.05f + (warpDrive * 0.10f)))) * (4f + (warpAmount * 5f));

        float n = Hlsl.Pow(Hlsl.Abs(FBm((p * (0.06f + (warpDrive * 0.04f))) + new float3(0f, time * (0.04f + (warpDrive * 0.08f)), 0f))) * 12f, 1.3f);
        float s = FBm((p * 0.01f) + new float3(0f, time * (0.14f + (bassDrive * 0.10f)), 0f)) * 128f;

        float dist = Hlsl.Max(0f, -FCylinderInf(p, s + (18f * tunnelBaseRadius / 0.22f) - n));

        p.X -= (Hlsl.Sin((p.Y * (0.02f + (warpAmount * 0.02f))) + (time * (0.25f + (warpDrive * 0.45f)))) * (34f + (warpAmount * 120f)))
            + (Hlsl.Cos((p.Z * 0.01f) + (time * (0.18f + (warpDrive * 0.20f)))) * (62f + (warpAmount * 42f)));

        dist = Hlsl.Max(dist, -FCylinderInf(p, s + (28f * tunnelDepth) + (n * (2f + (warpDrive * 1.50f)))));

        return dist;
    }

    private float2 Trace(float3 o, float3 d)
    {
        float omega = 1.3f;
        float t = 10f;
        float candidateError = Infinity;
        float candidateT = 10f;
        float previousRadius = 0f;
        float stepLength = 0f;
        float pixelRadius = 1f / 1000f;
        float iterationValue = 0f;

        float functionSign = Map(o) < 0f ? -1f : 1f;
        int iterationLimit = ClampInt(traceIterations, 24, MaxIterations);

        for (int i = 0; i < MaxIterations; i++)
        {
            if (i >= iterationLimit)
            {
                break;
            }

            float dist = Map((d * t) + o);
            float signedRadius = functionSign * dist;
            float radius = Hlsl.Abs(signedRadius);
            bool sorFail = omega > 1f && (radius + previousRadius) < stepLength;

            if (sorFail)
            {
                stepLength -= omega * stepLength;
                omega = 1f;
            }
            else
            {
                stepLength = signedRadius * omega;
            }

            previousRadius = radius;
            float error = radius / Hlsl.Max(t, 0.0001f);

            if (!sorFail && error < candidateError)
            {
                candidateT = t;
                candidateError = error;
                iterationValue = i;
            }

            if ((!sorFail && error < pixelRadius) || t > Far)
            {
                break;
            }

            t += stepLength * 0.5f;
        }

        if (t > Far || candidateError > pixelRadius)
        {
            return new float2(Infinity, Hlsl.Min(0.95f, iterationValue / 90f));
        }

        return new float2(candidateT, Hlsl.Min(0.95f, iterationValue / 90f));
    }

    private static int ClampInt(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
