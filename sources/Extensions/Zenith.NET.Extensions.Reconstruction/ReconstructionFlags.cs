namespace Zenith.NET.Extensions.Reconstruction;

[Flags]
public enum ReconstructionFlags
{
    None = 0,

    DepthReversed = 1 << 0,

    AutoExposure = 1 << 1,

    JitteredMotionVectors = 1 << 2,

    OutputResolutionMotionVectors = 1 << 3
}
