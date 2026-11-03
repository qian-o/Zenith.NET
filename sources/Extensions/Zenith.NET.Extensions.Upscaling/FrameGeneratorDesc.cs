namespace Zenith.NET.Extensions.Upscaling;

public struct FrameGeneratorDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat? UIFormat;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public UpscalingInputFlags Flags;
}
