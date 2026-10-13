namespace Zenith.NET.Extensions.DLSS;

public struct DLSSSuperResolutionDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DLSSMode Mode;

    public DLSSFeatureFlags Flags;
}
