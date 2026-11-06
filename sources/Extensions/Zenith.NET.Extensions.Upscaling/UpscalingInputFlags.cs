namespace Zenith.NET.Extensions.Upscaling;

[Flags]
public enum UpscalingInputFlags
{
    None = 0,

    DepthReversed = 1 << 0,

    AutoExposure = 1 << 1,

    DepthInfinite = 1 << 2
}
