namespace Zenith.NET.Extensions.DLSS;

public struct DLSSFrameGenerationDesc
{
    public PixelFormat Format;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public bool IsHdr;

    public bool IsDepthReversed;

    public bool IsMotionVectorJittered;

    public bool IsDynamicResolutionEnabled;

    public bool IsUIRecompositionEnabled;
}
