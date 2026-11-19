using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal static class FSRFormats
{
    public static FSRApiSurfaceFormat FSR(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.R8UNorm => FSRApiSurfaceFormat.R8Unorm,
            PixelFormat.R8SNorm => FSRApiSurfaceFormat.R8Snorm,
            PixelFormat.R8UInt => FSRApiSurfaceFormat.R8Uint,

            PixelFormat.R16UNorm => FSRApiSurfaceFormat.R16Unorm,
            PixelFormat.R16SNorm => FSRApiSurfaceFormat.R16Snorm,
            PixelFormat.R16UInt => FSRApiSurfaceFormat.R16Uint,
            PixelFormat.R16Float => FSRApiSurfaceFormat.R16Float,

            PixelFormat.R32UInt => FSRApiSurfaceFormat.R32Uint,
            PixelFormat.R32Float => FSRApiSurfaceFormat.R32Float,

            PixelFormat.R8G8UNorm => FSRApiSurfaceFormat.R8G8Unorm,
            PixelFormat.R8G8UInt => FSRApiSurfaceFormat.R8G8Uint,

            PixelFormat.R16G16UInt => FSRApiSurfaceFormat.R16G16Uint,
            PixelFormat.R16G16SInt => FSRApiSurfaceFormat.R16G16Sint,
            PixelFormat.R16G16Float => FSRApiSurfaceFormat.R16G16Float,

            PixelFormat.R32G32UInt => FSRApiSurfaceFormat.R32G32Uint,
            PixelFormat.R32G32Float => FSRApiSurfaceFormat.R32G32Float,

            PixelFormat.R9G9B9E5Float => FSRApiSurfaceFormat.R9G9B9E5SharedExp,

            PixelFormat.R11G11B10Float => FSRApiSurfaceFormat.R11G11B10Float,

            PixelFormat.R8G8B8A8UNorm => FSRApiSurfaceFormat.R8G8B8A8Unorm,
            PixelFormat.R8G8B8A8SNorm => FSRApiSurfaceFormat.R8G8B8A8Snorm,
            PixelFormat.R8G8B8A8SRgb => FSRApiSurfaceFormat.R8G8B8A8Srgb,

            PixelFormat.R10G10B10A2UNorm => FSRApiSurfaceFormat.R10G10B10A2Unorm,

            PixelFormat.R16G16B16A16Float => FSRApiSurfaceFormat.R16G16B16A16Float,

            PixelFormat.R32G32B32A32UInt => FSRApiSurfaceFormat.R32G32B32A32Uint,
            PixelFormat.R32G32B32A32Float => FSRApiSurfaceFormat.R32G32B32A32Float,

            PixelFormat.B8G8R8A8UNorm => FSRApiSurfaceFormat.B8G8R8A8Unorm,
            PixelFormat.B8G8R8A8SRgb => FSRApiSurfaceFormat.B8G8R8A8Srgb,

            PixelFormat.D16UNorm => FSRApiSurfaceFormat.R16Unorm,
            PixelFormat.D32Float => FSRApiSurfaceFormat.R32Float,
            PixelFormat.D32FloatS8UInt => FSRApiSurfaceFormat.R32Float,

            _ => default
        };
    }

    public static FSRApiResourceType FSR(TextureType textureType)
    {
        return textureType switch
        {
            TextureType.Texture1D or
            TextureType.Texture1DArray => FSRApiResourceType.Texture1D,

            TextureType.Texture2D or
            TextureType.Texture2DArray => FSRApiResourceType.Texture2D,

            TextureType.Texture3D => FSRApiResourceType.Texture3D,

            TextureType.TextureCube or
            TextureType.TextureCubeArray => FSRApiResourceType.TextureCube,

            _ => default
        };
    }

    public static FSRApiResourceUsage FSR(TextureUsages textureUsages)
    {
        FSRApiResourceUsage result = default;

        if (textureUsages.HasFlag(TextureUsages.Storage))
        {
            result |= FSRApiResourceUsage.Uav;
        }

        if (textureUsages.HasFlag(TextureUsages.ColorAttachment))
        {
            result |= FSRApiResourceUsage.RenderTarget;
        }

        if (textureUsages.HasFlag(TextureUsages.DepthStencilAttachment))
        {
            result |= FSRApiResourceUsage.DepthTarget;
        }

        return result;
    }
}
