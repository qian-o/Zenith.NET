namespace Zenith.NET.Extensions.Upscaling;

public struct SuperResolutionDesc
{
    public PixelFormat Format;

    public uint RenderWidth;

    public uint RenderHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public static SuperResolutionDesc Quality(GraphicsContext context, PixelFormat format, uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        return context.UpscalingBridge.CreateSuperResolutionDesc(format, outputWidth, outputHeight, quality);
    }
}
