using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal static class MetalFXFormats
{
    public static MTLPixelFormat Metal(PixelFormat? pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.A8UNorm => MTLPixelFormat.A8Unorm,

            PixelFormat.R8UNorm => MTLPixelFormat.R8Unorm,
            PixelFormat.R8SNorm => MTLPixelFormat.R8Snorm,
            PixelFormat.R8UInt => MTLPixelFormat.R8Uint,
            PixelFormat.R8SInt => MTLPixelFormat.R8Sint,

            PixelFormat.R16UNorm => MTLPixelFormat.R16Unorm,
            PixelFormat.R16SNorm => MTLPixelFormat.R16Snorm,
            PixelFormat.R16UInt => MTLPixelFormat.R16Uint,
            PixelFormat.R16SInt => MTLPixelFormat.R16Sint,
            PixelFormat.R16Float => MTLPixelFormat.R16Float,

            PixelFormat.R32UInt => MTLPixelFormat.R32Uint,
            PixelFormat.R32SInt => MTLPixelFormat.R32Sint,
            PixelFormat.R32Float => MTLPixelFormat.R32Float,

            PixelFormat.R8G8UNorm => MTLPixelFormat.RG8Unorm,
            PixelFormat.R8G8SNorm => MTLPixelFormat.RG8Snorm,
            PixelFormat.R8G8UInt => MTLPixelFormat.RG8Uint,
            PixelFormat.R8G8SInt => MTLPixelFormat.RG8Sint,

            PixelFormat.R16G16UNorm => MTLPixelFormat.RG16Unorm,
            PixelFormat.R16G16SNorm => MTLPixelFormat.RG16Snorm,
            PixelFormat.R16G16UInt => MTLPixelFormat.RG16Uint,
            PixelFormat.R16G16SInt => MTLPixelFormat.RG16Sint,
            PixelFormat.R16G16Float => MTLPixelFormat.RG16Float,

            PixelFormat.R32G32UInt => MTLPixelFormat.RG32Uint,
            PixelFormat.R32G32SInt => MTLPixelFormat.RG32Sint,
            PixelFormat.R32G32Float => MTLPixelFormat.RG32Float,

            PixelFormat.R9G9B9E5Float => MTLPixelFormat.RGB9E5Float,

            PixelFormat.R11G11B10Float => MTLPixelFormat.RG11B10Float,

            PixelFormat.R8G8B8A8UNorm => MTLPixelFormat.RGBA8Unorm,
            PixelFormat.R8G8B8A8SNorm => MTLPixelFormat.RGBA8Snorm,
            PixelFormat.R8G8B8A8UInt => MTLPixelFormat.RGBA8Uint,
            PixelFormat.R8G8B8A8SInt => MTLPixelFormat.RGBA8Sint,
            PixelFormat.R8G8B8A8SRgb => MTLPixelFormat.RGBA8Unorm_sRGB,

            PixelFormat.R10G10B10A2UNorm => MTLPixelFormat.RGB10A2Unorm,
            PixelFormat.R10G10B10A2UInt => MTLPixelFormat.RGB10A2Uint,

            PixelFormat.R16G16B16A16UNorm => MTLPixelFormat.RGBA16Unorm,
            PixelFormat.R16G16B16A16SNorm => MTLPixelFormat.RGBA16Snorm,
            PixelFormat.R16G16B16A16UInt => MTLPixelFormat.RGBA16Uint,
            PixelFormat.R16G16B16A16SInt => MTLPixelFormat.RGBA16Sint,
            PixelFormat.R16G16B16A16Float => MTLPixelFormat.RGBA16Float,

            PixelFormat.R32G32B32A32UInt => MTLPixelFormat.RGBA32Uint,
            PixelFormat.R32G32B32A32SInt => MTLPixelFormat.RGBA32Sint,
            PixelFormat.R32G32B32A32Float => MTLPixelFormat.RGBA32Float,

            PixelFormat.B8G8R8A8UNorm => MTLPixelFormat.BGRA8Unorm,
            PixelFormat.B8G8R8A8SRgb => MTLPixelFormat.BGRA8Unorm_sRGB,

            PixelFormat.D16UNorm => MTLPixelFormat.Depth16Unorm,
            PixelFormat.D32Float => MTLPixelFormat.Depth32Float,
            PixelFormat.D32FloatS8UInt => MTLPixelFormat.Depth32Float_Stencil8,

            _ => default
        };
    }
}
