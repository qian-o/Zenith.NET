namespace Zenith.NET.Extensions.DLSS;

public struct DLSSRayReconstructionDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DLSSMode Mode;

    public bool IsDepthReversed;

    public bool IsDepthLinear;

    public bool IsRoughnessPacked;

    public bool IsMotionVectorJittered;

    public bool IsAlphaUpscalingEnabled;
}
