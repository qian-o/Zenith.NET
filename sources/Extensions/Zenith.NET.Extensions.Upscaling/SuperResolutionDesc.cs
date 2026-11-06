namespace Zenith.NET.Extensions.Upscaling;

public struct SuperResolutionDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat? ReactiveMaskFormat;

    public PixelFormat OutputFormat;

    public bool IsHdr;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public UpscalingInputFlags Flags;
}
