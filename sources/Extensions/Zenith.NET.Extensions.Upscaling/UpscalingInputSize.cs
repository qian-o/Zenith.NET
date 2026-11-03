namespace Zenith.NET.Extensions.Upscaling;

public readonly struct UpscalerInputSize(uint width, uint height)
{
    public readonly uint Width = width;

    public readonly uint Height = height;
}
