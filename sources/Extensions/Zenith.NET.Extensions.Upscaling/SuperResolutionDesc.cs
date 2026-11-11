namespace Zenith.NET.Extensions.Upscaling;

public struct SuperResolutionDesc
{
    public uint RenderWidth;

    public uint RenderHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public static SuperResolutionDesc Quality(GraphicsContext context, uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        return context.UpscalingBridge.CreateSuperResolutionDesc(outputWidth, outputHeight, quality);
    }
}
