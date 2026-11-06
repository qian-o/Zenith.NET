namespace Zenith.NET.Extensions.Upscaling;

public struct FrameGenerationDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat? HudlessFormat;

    public PixelFormat OutputFormat;

    public bool IsHdr;

    public uint Width;

    public uint Height;

    public uint AuxiliaryWidth;

    public uint AuxiliaryHeight;

    public UpscalingInputFlags Flags;
}
