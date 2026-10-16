namespace Zenith.NET.Extensions.DLSS;

[Flags]
public enum DLSSSuperResolutionFlags
{
    None = 0,

    AutoExposure = 1 << 0,

    DepthReversed = 1 << 1
}
