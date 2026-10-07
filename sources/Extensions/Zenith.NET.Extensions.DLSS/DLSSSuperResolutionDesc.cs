namespace Zenith.NET.Extensions.DLSS;

public struct DLSSSuperResolutionDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DLSSMode Mode;

    public bool IsHdr;

    public bool IsAutoExposureEnabled;

    public bool IsDepthReversed;

    public bool IsMotionVectorJittered;

    public bool IsAlphaUpscalingEnabled;
}
