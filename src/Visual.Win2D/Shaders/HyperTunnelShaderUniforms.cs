using System.Numerics;

namespace Visual.Win2D.Shaders;

// DOCS: docs/wiki/modules/visual-win2d.md#hyper-tunnel-shader-gpu
internal readonly record struct HyperTunnelShaderUniforms(
    float Time,
    float AudioActivityTime,
    float Bass,
    float Mid,
    float High,
    float Level,
    float Band32,
    float Band128,
    float TunnelBaseRadius,
    float TunnelDepth,
    float TunnelSpeed,
    float TunnelWarp,
    float TunnelFogAmount,
    Vector3 WarmColor,
    Vector3 CoolColor);
