namespace Zenith.NET.Extensions.DLSS;

[Flags]
public enum DLSSRayReconstructionFlags
{
    None = 0,

    DepthReversed = 1 << 0
}
