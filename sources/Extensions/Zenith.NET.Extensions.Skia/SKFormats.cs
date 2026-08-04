using SkiaSharp;

namespace Zenith.NET.Extensions.Skia;

internal static class SKFormats
{
    public static uint DirectX12(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.R8UNorm => 61,
            PixelFormat.R16Float => 54,
            PixelFormat.R8G8B8A8UNorm => 28,
            PixelFormat.R8G8B8A8SRgb => 29,
            PixelFormat.R16G16B16A16Float => 10,
            PixelFormat.R32G32B32A32Float => 2,
            PixelFormat.B8G8R8A8UNorm => 87,
            _ => default
        };
    }

    public static SKColorType Skia(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.R8UNorm => SKColorType.Gray8,
            PixelFormat.R16Float => SKColorType.AlphaF16,
            PixelFormat.R8G8B8A8UNorm => SKColorType.Rgba8888,
            PixelFormat.R8G8B8A8SRgb => SKColorType.Srgba8888,
            PixelFormat.R16G16B16A16Float => SKColorType.RgbaF16,
            PixelFormat.R32G32B32A32Float => SKColorType.RgbaF32,
            PixelFormat.B8G8R8A8UNorm => SKColorType.Bgra8888,
            _ => default
        };
    }

    public static uint Vulkan(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.R8UNorm => 9,
            PixelFormat.R16Float => 76,
            PixelFormat.R8G8B8A8UNorm => 37,
            PixelFormat.R8G8B8A8SRgb => 43,
            PixelFormat.R16G16B16A16Float => 97,
            PixelFormat.R32G32B32A32Float => 109,
            PixelFormat.B8G8R8A8UNorm => 44,
            _ => default
        };
    }

    public static uint Skia(SampleCount sampleCount)
    {
        return sampleCount switch
        {
            SampleCount.Count1 => 1,
            SampleCount.Count2 => 2,
            SampleCount.Count4 => 4,
            SampleCount.Count8 => 8,
            SampleCount.Count16 => 16,
            SampleCount.Count32 => 32,
            _ => default
        };
    }

    public static uint Vulkan(TextureUsages textureUsages)
    {
        uint result = default;

        if (textureUsages.HasFlag(TextureUsages.Sampled))
        {
            result |= 1 << 2;
        }

        if (textureUsages.HasFlag(TextureUsages.Storage))
        {
            result |= 1 << 3;
        }

        if (textureUsages.HasFlag(TextureUsages.ColorAttachment))
        {
            result |= 1 << 4;
        }

        if (textureUsages.HasFlag(TextureUsages.DepthStencilAttachment))
        {
            result |= 1 << 5;
        }

        if (textureUsages.HasFlag(TextureUsages.TransferSrc))
        {
            result |= 1 << 0;
        }

        if (textureUsages.HasFlag(TextureUsages.TransferDst))
        {
            result |= 1 << 1;
        }

        return result;
    }
}
