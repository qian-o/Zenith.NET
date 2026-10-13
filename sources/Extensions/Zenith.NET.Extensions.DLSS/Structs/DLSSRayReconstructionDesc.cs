namespace Zenith.NET.Extensions.DLSS;

public struct DLSSRayReconstructionDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DLSSMode Mode;

    public DLSSDepthType DepthType;

    public DLSSRoughnessMode RoughnessMode;

    public DLSSFeatureFlags Flags;
}
