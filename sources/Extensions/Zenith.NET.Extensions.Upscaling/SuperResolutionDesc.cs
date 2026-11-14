namespace Zenith.NET.Extensions.Upscaling;

public struct SuperResolutionDesc
{
    public PixelFormat Format;

    public uint RenderWidth;

    public uint RenderHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public static SuperResolutionDesc Preset(PixelFormat format, uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        (uint renderWidth, uint renderHeight) = UpscalingHelper.RenderSize(outputWidth, outputHeight, quality);

        return new()
        {
            Format = format,
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            OutputWidth = outputWidth,
            OutputHeight = outputHeight
        };
    }
}
