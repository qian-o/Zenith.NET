namespace Zenith.NET.Extensions.DLSS;

[Flags]
public enum DLSSFeatureFlags
{
    None = 0,

    Hdr = 1 << 0,

    AutoExposure = 1 << 1,

    DepthReversed = 1 << 2,

    MotionVectorJittered = 1 << 3,

    AlphaUpscaling = 1 << 4,

    DynamicResolution = 1 << 5,

    UIRecomposition = 1 << 6
}
