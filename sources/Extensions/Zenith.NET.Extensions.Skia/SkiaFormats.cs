using SkiaSharp;

namespace Zenith.NET.Extensions.Skia;

internal static class SkiaFormats
{
    public static SKColorType Skia(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.A8UNorm => SKColorType.Alpha8,
            PixelFormat.R8UNorm => SKColorType.R8Unorm,
            PixelFormat.R16UNorm => SKColorType.R16Unorm,
            PixelFormat.R16Float => SKColorType.RF16,
            PixelFormat.R8G8UNorm => SKColorType.Rg88,
            PixelFormat.R16G16UNorm => SKColorType.Rg1616,
            PixelFormat.R16G16Float => SKColorType.RgF16,
            PixelFormat.R8G8B8A8UNorm => SKColorType.Rgba8888,
            PixelFormat.R8G8B8A8SRgb => SKColorType.Srgba8888,
            PixelFormat.R10G10B10A2UNorm => SKColorType.Rgba1010102,
            PixelFormat.R16G16B16A16UNorm => SKColorType.Rgba16161616,
            PixelFormat.R16G16B16A16Float => SKColorType.RgbaF16,
            PixelFormat.R32G32B32A32Float => SKColorType.RgbaF32,
            PixelFormat.B8G8R8A8UNorm => SKColorType.Bgra8888,
            _ => default
        };
    }

    public static uint DirectX12(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.A8UNorm => 65,
            PixelFormat.R8UNorm => 61,
            PixelFormat.R16UNorm => 56,
            PixelFormat.R16Float => 54,
            PixelFormat.R8G8UNorm => 49,
            PixelFormat.R16G16UNorm => 35,
            PixelFormat.R16G16Float => 34,
            PixelFormat.R8G8B8A8UNorm => 28,
            PixelFormat.R8G8B8A8SRgb => 29,
            PixelFormat.R10G10B10A2UNorm => 24,
            PixelFormat.R16G16B16A16UNorm => 11,
            PixelFormat.R16G16B16A16Float => 10,
            PixelFormat.R32G32B32A32Float => 2,
            PixelFormat.B8G8R8A8UNorm => 87,
            _ => default
        };
    }

    public static uint Vulkan(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.A8UNorm => 1000470001,
            PixelFormat.R8UNorm => 9,
            PixelFormat.R16UNorm => 70,
            PixelFormat.R16Float => 76,
            PixelFormat.R8G8UNorm => 16,
            PixelFormat.R16G16UNorm => 77,
            PixelFormat.R16G16Float => 83,
            PixelFormat.R8G8B8A8UNorm => 37,
            PixelFormat.R8G8B8A8SRgb => 43,
            PixelFormat.R10G10B10A2UNorm => 64,
            PixelFormat.R16G16B16A16UNorm => 91,
            PixelFormat.R16G16B16A16Float => 97,
            PixelFormat.R32G32B32A32Float => 109,
            PixelFormat.B8G8R8A8UNorm => 44,
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
