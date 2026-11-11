namespace Zenith.NET.Extensions.Upscaling;

public struct RayReconstructionDesc
{
    public uint RenderWidth;

    public uint RenderHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public static RayReconstructionDesc Quality(GraphicsContext context, uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        return context.UpscalingBridge.CreateRayReconstructionDesc(outputWidth, outputHeight, quality);
    }
}
