using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal static class DLSSFormats
{
    public static uint DirectX12(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.A8UNorm => 65,

            PixelFormat.R8UNorm => 61,
            PixelFormat.R8SNorm => 63,
            PixelFormat.R8UInt => 62,
            PixelFormat.R8SInt => 64,

            PixelFormat.R16UNorm => 56,
            PixelFormat.R16SNorm => 58,
            PixelFormat.R16UInt => 57,
            PixelFormat.R16SInt => 59,
            PixelFormat.R16Float => 54,

            PixelFormat.R32UInt => 42,
            PixelFormat.R32SInt => 43,
            PixelFormat.R32Float => 41,

            PixelFormat.R8G8UNorm => 49,
            PixelFormat.R8G8SNorm => 51,
            PixelFormat.R8G8UInt => 50,
            PixelFormat.R8G8SInt => 52,

            PixelFormat.R16G16UNorm => 35,
            PixelFormat.R16G16SNorm => 37,
            PixelFormat.R16G16UInt => 36,
            PixelFormat.R16G16SInt => 38,
            PixelFormat.R16G16Float => 34,

            PixelFormat.R32G32UInt => 17,
            PixelFormat.R32G32SInt => 18,
            PixelFormat.R32G32Float => 16,

            PixelFormat.R9G9B9E5Float => 67,

            PixelFormat.R11G11B10Float => 26,

            PixelFormat.R8G8B8A8UNorm => 28,
            PixelFormat.R8G8B8A8SNorm => 31,
            PixelFormat.R8G8B8A8UInt => 30,
            PixelFormat.R8G8B8A8SInt => 32,
            PixelFormat.R8G8B8A8SRgb => 29,

            PixelFormat.R10G10B10A2UNorm => 24,
            PixelFormat.R10G10B10A2UInt => 25,

            PixelFormat.R16G16B16A16UNorm => 11,
            PixelFormat.R16G16B16A16SNorm => 13,
            PixelFormat.R16G16B16A16UInt => 12,
            PixelFormat.R16G16B16A16SInt => 14,
            PixelFormat.R16G16B16A16Float => 10,

            PixelFormat.R32G32B32A32UInt => 3,
            PixelFormat.R32G32B32A32SInt => 4,
            PixelFormat.R32G32B32A32Float => 2,

            PixelFormat.B8G8R8A8UNorm => 87,
            PixelFormat.B8G8R8A8SRgb => 91,

            PixelFormat.D16UNorm => 55,
            PixelFormat.D32Float => 40,
            PixelFormat.D32FloatS8UInt => 20,

            PixelFormat.BC1UNorm => 71,
            PixelFormat.BC1SRgb => 72,
            PixelFormat.BC2UNorm => 74,
            PixelFormat.BC2SRgb => 75,
            PixelFormat.BC3UNorm => 77,
            PixelFormat.BC3SRgb => 78,
            PixelFormat.BC4UNorm => 80,
            PixelFormat.BC4SNorm => 81,
            PixelFormat.BC5UNorm => 83,
            PixelFormat.BC5SNorm => 84,
            PixelFormat.BC6HUFloat => 95,
            PixelFormat.BC6HSFloat => 96,
            PixelFormat.BC7UNorm => 98,
            PixelFormat.BC7SRgb => 99,

            _ => default
        };
    }

    public static NGXVkFormat Vulkan(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.A8UNorm => NGXVkFormat.A8Unorm,

            PixelFormat.R8UNorm => NGXVkFormat.R8Unorm,
            PixelFormat.R8SNorm => NGXVkFormat.R8Snorm,
            PixelFormat.R8UInt => NGXVkFormat.R8Uint,
            PixelFormat.R8SInt => NGXVkFormat.R8Sint,

            PixelFormat.R16UNorm => NGXVkFormat.R16Unorm,
            PixelFormat.R16SNorm => NGXVkFormat.R16Snorm,
            PixelFormat.R16UInt => NGXVkFormat.R16Uint,
            PixelFormat.R16SInt => NGXVkFormat.R16Sint,
            PixelFormat.R16Float => NGXVkFormat.R16Sfloat,

            PixelFormat.R32UInt => NGXVkFormat.R32Uint,
            PixelFormat.R32SInt => NGXVkFormat.R32Sint,
            PixelFormat.R32Float => NGXVkFormat.R32Sfloat,

            PixelFormat.R8G8UNorm => NGXVkFormat.R8G8Unorm,
            PixelFormat.R8G8SNorm => NGXVkFormat.R8G8Snorm,
            PixelFormat.R8G8UInt => NGXVkFormat.R8G8Uint,
            PixelFormat.R8G8SInt => NGXVkFormat.R8G8Sint,

            PixelFormat.R16G16UNorm => NGXVkFormat.R16G16Unorm,
            PixelFormat.R16G16SNorm => NGXVkFormat.R16G16Snorm,
            PixelFormat.R16G16UInt => NGXVkFormat.R16G16Uint,
            PixelFormat.R16G16SInt => NGXVkFormat.R16G16Sint,
            PixelFormat.R16G16Float => NGXVkFormat.R16G16Sfloat,

            PixelFormat.R32G32UInt => NGXVkFormat.R32G32Uint,
            PixelFormat.R32G32SInt => NGXVkFormat.R32G32Sint,
            PixelFormat.R32G32Float => NGXVkFormat.R32G32Sfloat,

            PixelFormat.R9G9B9E5Float => NGXVkFormat.E5B9G9R9UfloatPack32,

            PixelFormat.R11G11B10Float => NGXVkFormat.B10G11R11UfloatPack32,

            PixelFormat.R8G8B8A8UNorm => NGXVkFormat.R8G8B8A8Unorm,
            PixelFormat.R8G8B8A8SNorm => NGXVkFormat.R8G8B8A8Snorm,
            PixelFormat.R8G8B8A8UInt => NGXVkFormat.R8G8B8A8Uint,
            PixelFormat.R8G8B8A8SInt => NGXVkFormat.R8G8B8A8Sint,
            PixelFormat.R8G8B8A8SRgb => NGXVkFormat.R8G8B8A8Srgb,

            PixelFormat.R10G10B10A2UNorm => NGXVkFormat.A2B10G10R10UnormPack32,
            PixelFormat.R10G10B10A2UInt => NGXVkFormat.A2B10G10R10UintPack32,

            PixelFormat.R16G16B16A16UNorm => NGXVkFormat.R16G16B16A16Unorm,
            PixelFormat.R16G16B16A16SNorm => NGXVkFormat.R16G16B16A16Snorm,
            PixelFormat.R16G16B16A16UInt => NGXVkFormat.R16G16B16A16Uint,
            PixelFormat.R16G16B16A16SInt => NGXVkFormat.R16G16B16A16Sint,
            PixelFormat.R16G16B16A16Float => NGXVkFormat.R16G16B16A16Sfloat,

            PixelFormat.R32G32B32A32UInt => NGXVkFormat.R32G32B32A32Uint,
            PixelFormat.R32G32B32A32SInt => NGXVkFormat.R32G32B32A32Sint,
            PixelFormat.R32G32B32A32Float => NGXVkFormat.R32G32B32A32Sfloat,

            PixelFormat.B8G8R8A8UNorm => NGXVkFormat.B8G8R8A8Unorm,
            PixelFormat.B8G8R8A8SRgb => NGXVkFormat.B8G8R8A8Srgb,

            PixelFormat.D16UNorm => NGXVkFormat.D16Unorm,
            PixelFormat.D32Float => NGXVkFormat.D32Sfloat,
            PixelFormat.D32FloatS8UInt => NGXVkFormat.D32SfloatS8Uint,

            PixelFormat.BC1UNorm => NGXVkFormat.Bc1RgbaUnormBlock,
            PixelFormat.BC1SRgb => NGXVkFormat.Bc1RgbaSrgbBlock,
            PixelFormat.BC2UNorm => NGXVkFormat.Bc2UnormBlock,
            PixelFormat.BC2SRgb => NGXVkFormat.Bc2SrgbBlock,
            PixelFormat.BC3UNorm => NGXVkFormat.Bc3UnormBlock,
            PixelFormat.BC3SRgb => NGXVkFormat.Bc3SrgbBlock,
            PixelFormat.BC4UNorm => NGXVkFormat.Bc4UnormBlock,
            PixelFormat.BC4SNorm => NGXVkFormat.Bc4SnormBlock,
            PixelFormat.BC5UNorm => NGXVkFormat.Bc5UnormBlock,
            PixelFormat.BC5SNorm => NGXVkFormat.Bc5SnormBlock,
            PixelFormat.BC6HUFloat => NGXVkFormat.Bc6HUfloatBlock,
            PixelFormat.BC6HSFloat => NGXVkFormat.Bc6HSfloatBlock,
            PixelFormat.BC7UNorm => NGXVkFormat.Bc7UnormBlock,
            PixelFormat.BC7SRgb => NGXVkFormat.Bc7SrgbBlock,

            PixelFormat.EACR11UNorm => NGXVkFormat.EacR11UnormBlock,
            PixelFormat.EACR11SNorm => NGXVkFormat.EacR11SnormBlock,
            PixelFormat.EACR11G11UNorm => NGXVkFormat.EacR11G11UnormBlock,
            PixelFormat.EACR11G11SNorm => NGXVkFormat.EacR11G11SnormBlock,

            PixelFormat.ETC2UNorm => NGXVkFormat.Etc2R8G8B8UnormBlock,
            PixelFormat.ETC2SRgb => NGXVkFormat.Etc2R8G8B8SrgbBlock,
            PixelFormat.ETC2A1UNorm => NGXVkFormat.Etc2R8G8B8A1UnormBlock,
            PixelFormat.ETC2A1SRgb => NGXVkFormat.Etc2R8G8B8A1SrgbBlock,
            PixelFormat.ETC2A8UNorm => NGXVkFormat.Etc2R8G8B8A8UnormBlock,
            PixelFormat.ETC2A8SRgb => NGXVkFormat.Etc2R8G8B8A8SrgbBlock,

            PixelFormat.ASTC4x4UNorm => NGXVkFormat.Astc4X4UnormBlock,
            PixelFormat.ASTC4x4SRgb => NGXVkFormat.Astc4X4SrgbBlock,
            PixelFormat.ASTC4x4Float => NGXVkFormat.Astc4X4SfloatBlock,
            PixelFormat.ASTC5x5UNorm => NGXVkFormat.Astc5X5UnormBlock,
            PixelFormat.ASTC5x5SRgb => NGXVkFormat.Astc5X5SrgbBlock,
            PixelFormat.ASTC5x5Float => NGXVkFormat.Astc5X5SfloatBlock,
            PixelFormat.ASTC6x6UNorm => NGXVkFormat.Astc6X6UnormBlock,
            PixelFormat.ASTC6x6SRgb => NGXVkFormat.Astc6X6SrgbBlock,
            PixelFormat.ASTC6x6Float => NGXVkFormat.Astc6X6SfloatBlock,
            PixelFormat.ASTC8x8UNorm => NGXVkFormat.Astc8X8UnormBlock,
            PixelFormat.ASTC8x8SRgb => NGXVkFormat.Astc8X8SrgbBlock,
            PixelFormat.ASTC8x8Float => NGXVkFormat.Astc8X8SfloatBlock,
            PixelFormat.ASTC10x10UNorm => NGXVkFormat.Astc10X10UnormBlock,
            PixelFormat.ASTC10x10SRgb => NGXVkFormat.Astc10X10SrgbBlock,
            PixelFormat.ASTC10x10Float => NGXVkFormat.Astc10X10SfloatBlock,
            PixelFormat.ASTC12x12UNorm => NGXVkFormat.Astc12X12UnormBlock,
            PixelFormat.ASTC12x12SRgb => NGXVkFormat.Astc12X12SrgbBlock,
            PixelFormat.ASTC12x12Float => NGXVkFormat.Astc12X12SfloatBlock,

            _ => default
        };
    }
}
