namespace Zenith.NET.Extensions.Reconstruction;

public struct TemporalUpscalerDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat? ReactiveMaskFormat;

    public PixelFormat OutputFormat;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public ReconstructionFlags Flags;
}
