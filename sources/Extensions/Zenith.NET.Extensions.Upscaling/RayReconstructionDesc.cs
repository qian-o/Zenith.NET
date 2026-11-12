namespace Zenith.NET.Extensions.Upscaling;

public struct RayReconstructionDesc
{
    public PixelFormat Format;

    public uint RenderWidth;

    public uint RenderHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public static RayReconstructionDesc Quality(GraphicsContext context, PixelFormat format, uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        return context.UpscalingBridge.CreateRayReconstructionDesc(format, outputWidth, outputHeight, quality);
    }
}
