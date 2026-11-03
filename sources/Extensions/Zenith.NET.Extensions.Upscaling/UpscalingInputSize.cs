namespace Zenith.NET.Extensions.Upscaling;

public readonly struct UpscalingInputSize(uint width, uint height)
{
    public readonly uint Width = width;

    public readonly uint Height = height;
}
