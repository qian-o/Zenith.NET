namespace Zenith.NET.Extensions.Upscaling;

public struct FrameGenerationSwapChainDesc
{
    public Surface Surface;

    public bool IsVerticalSyncEnabled;

    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat? HudlessFormat;

    public bool IsHdr;

    public uint AuxiliaryWidth;

    public uint AuxiliaryHeight;

    public UpscalingInputFlags Flags;
}
