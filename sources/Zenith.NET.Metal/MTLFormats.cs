using Metal.NET;

namespace Zenith.NET.Metal;

internal static class MTLFormats
{
    public static MTLResourceOptions Metal(BufferUsageFlags bufferUsageFlags)
    {
        MTLResourceOptions result = MTLResourceOptions.HazardTrackingModeUntracked;

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.MapRead) || bufferUsageFlags.HasFlag(BufferUsageFlags.MapWrite))
        {
            result |= MTLResourceOptions.StorageModeShared;

            if (bufferUsageFlags.HasFlag(BufferUsageFlags.MapWrite))
            {
                result |= MTLResourceOptions.CPUCacheModeWriteCombined;
            }
        }
        else
        {
            result |= MTLResourceOptions.StorageModePrivate;
        }

        return result;
    }

    public static MTLTextureType Metal(TextureType textureType)
    {
        return textureType switch
        {
            TextureType.Texture1D => MTLTextureType.MTL1D,
            TextureType.Texture1DArray => MTLTextureType.MTL1DArray,
            TextureType.Texture2D => MTLTextureType.MTL2D,
            TextureType.Texture2DArray => MTLTextureType.MTL2DArray,
            TextureType.Texture3D => MTLTextureType.MTL3D,
            TextureType.TextureCube => MTLTextureType.MTLCube,
            TextureType.TextureCubeArray => MTLTextureType.MTLCubeArray,
            _ => MTLTextureType.MTL1D
        };
    }

    public static MTLPixelFormat Metal(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
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

            PixelFormat.R8G8B8A8UNorm => MTLPixelFormat.RGBA8Unorm,
            PixelFormat.R8G8B8A8SNorm => MTLPixelFormat.RGBA8Snorm,
            PixelFormat.R8G8B8A8UInt => MTLPixelFormat.RGBA8Uint,
            PixelFormat.R8G8B8A8SInt => MTLPixelFormat.RGBA8Sint,
            PixelFormat.R8G8B8A8SRgb => MTLPixelFormat.RGBA8Unorm_sRGB,

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
            PixelFormat.D24UNormS8UInt => MTLPixelFormat.Depth24Unorm_Stencil8,
            PixelFormat.D32Float => MTLPixelFormat.Depth32Float,
            PixelFormat.D32FloatS8UInt => MTLPixelFormat.Depth32Float_Stencil8,

            PixelFormat.BC4UNorm => MTLPixelFormat.BC4_RUnorm,
            PixelFormat.BC4SNorm => MTLPixelFormat.BC4_RSnorm,

            PixelFormat.BC5UNorm => MTLPixelFormat.BC5_RGUnorm,
            PixelFormat.BC5SNorm => MTLPixelFormat.BC5_RGSnorm,

            PixelFormat.BC6HUFloat => MTLPixelFormat.BC6H_RGBUfloat,
            PixelFormat.BC6HSFloat => MTLPixelFormat.BC6H_RGBFloat,

            PixelFormat.BC7UNorm => MTLPixelFormat.BC7_RGBAUnorm,
            PixelFormat.BC7SRgb => MTLPixelFormat.BC7_RGBAUnorm_sRGB,

            PixelFormat.ETC2UNorm => MTLPixelFormat.ETC2_RGB8,
            PixelFormat.ETC2SRgb => MTLPixelFormat.ETC2_RGB8_sRGB,

            PixelFormat.ETC2A1UNorm => MTLPixelFormat.ETC2_RGB8A1,
            PixelFormat.ETC2A1SRgb => MTLPixelFormat.ETC2_RGB8A1_sRGB,

            PixelFormat.ETC2A8UNorm => MTLPixelFormat.EAC_RGBA8,
            PixelFormat.ETC2A8SRgb => MTLPixelFormat.EAC_RGBA8_sRGB,

            PixelFormat.ASTC4x4UNorm => MTLPixelFormat.ASTC_4x4_LDR,
            PixelFormat.ASTC4x4SRgb => MTLPixelFormat.ASTC_4x4_sRGB,
            PixelFormat.ASTC4x4Float => MTLPixelFormat.ASTC_4x4_HDR,

            PixelFormat.ASTC5x5UNorm => MTLPixelFormat.ASTC_5x5_LDR,
            PixelFormat.ASTC5x5SRgb => MTLPixelFormat.ASTC_5x5_sRGB,
            PixelFormat.ASTC5x5Float => MTLPixelFormat.ASTC_5x5_HDR,

            PixelFormat.ASTC6x6UNorm => MTLPixelFormat.ASTC_6x6_LDR,
            PixelFormat.ASTC6x6SRgb => MTLPixelFormat.ASTC_6x6_sRGB,
            PixelFormat.ASTC6x6Float => MTLPixelFormat.ASTC_6x6_HDR,

            PixelFormat.ASTC8x8UNorm => MTLPixelFormat.ASTC_8x8_LDR,
            PixelFormat.ASTC8x8SRgb => MTLPixelFormat.ASTC_8x8_sRGB,
            PixelFormat.ASTC8x8Float => MTLPixelFormat.ASTC_8x8_HDR,

            PixelFormat.ASTC10x10UNorm => MTLPixelFormat.ASTC_10x10_LDR,
            PixelFormat.ASTC10x10SRgb => MTLPixelFormat.ASTC_10x10_sRGB,
            PixelFormat.ASTC10x10Float => MTLPixelFormat.ASTC_10x10_HDR,

            PixelFormat.ASTC12x12UNorm => MTLPixelFormat.ASTC_12x12_LDR,
            PixelFormat.ASTC12x12SRgb => MTLPixelFormat.ASTC_12x12_sRGB,
            PixelFormat.ASTC12x12Float => MTLPixelFormat.ASTC_12x12_HDR,

            _ => MTLPixelFormat.Invalid
        };
    }

    public static uint Metal(SampleCount sampleCount)
    {
        return sampleCount switch
        {
            SampleCount.Count1 => 1,
            SampleCount.Count2 => 2,
            SampleCount.Count4 => 4,
            SampleCount.Count8 => 8,
            SampleCount.Count16 => 16,
            SampleCount.Count32 => 32,
            _ => 1
        };
    }

    public static MTLTextureUsage Metal(TextureUsageFlags textureUsageFlags)
    {
        MTLTextureUsage result = MTLTextureUsage.Unknown;

        if (textureUsageFlags.HasFlag(TextureUsageFlags.RenderTarget) || textureUsageFlags.HasFlag(TextureUsageFlags.DepthStencil))
        {
            result |= MTLTextureUsage.RenderTarget;
        }

        if (textureUsageFlags.HasFlag(TextureUsageFlags.ShaderResource))
        {
            result |= MTLTextureUsage.ShaderRead;
        }

        if (textureUsageFlags.HasFlag(TextureUsageFlags.UnorderedAccess))
        {
            result |= MTLTextureUsage.ShaderWrite;
        }

        return result;
    }

    public static (MTLSamplerMinMagFilter MinFilter, MTLSamplerMinMagFilter MagFilter, MTLSamplerMipFilter MipFilter) Metal(Filter filter)
    {
        return filter switch
        {
            Filter.MinPointMagPointMipPoint => (MTLSamplerMinMagFilter.Nearest, MTLSamplerMinMagFilter.Nearest, MTLSamplerMipFilter.Nearest),
            Filter.MinPointMagPointMipLinear => (MTLSamplerMinMagFilter.Nearest, MTLSamplerMinMagFilter.Nearest, MTLSamplerMipFilter.Linear),
            Filter.MinPointMagLinearMipPoint => (MTLSamplerMinMagFilter.Nearest, MTLSamplerMinMagFilter.Linear, MTLSamplerMipFilter.Nearest),
            Filter.MinPointMagLinearMipLinear => (MTLSamplerMinMagFilter.Nearest, MTLSamplerMinMagFilter.Linear, MTLSamplerMipFilter.Linear),
            Filter.MinLinearMagPointMipPoint => (MTLSamplerMinMagFilter.Linear, MTLSamplerMinMagFilter.Nearest, MTLSamplerMipFilter.Nearest),
            Filter.MinLinearMagPointMipLinear => (MTLSamplerMinMagFilter.Linear, MTLSamplerMinMagFilter.Nearest, MTLSamplerMipFilter.Linear),
            Filter.MinLinearMagLinearMipPoint => (MTLSamplerMinMagFilter.Linear, MTLSamplerMinMagFilter.Linear, MTLSamplerMipFilter.Nearest),
            Filter.MinLinearMagLinearMipLinear => (MTLSamplerMinMagFilter.Linear, MTLSamplerMinMagFilter.Linear, MTLSamplerMipFilter.Linear),
            Filter.Anisotropic => (MTLSamplerMinMagFilter.Linear, MTLSamplerMinMagFilter.Linear, MTLSamplerMipFilter.Linear),
            _ => (MTLSamplerMinMagFilter.Nearest, MTLSamplerMinMagFilter.Nearest, MTLSamplerMipFilter.NotMipmapped)
        };
    }

    public static MTLSamplerAddressMode Metal(AddressMode addressMode)
    {
        return addressMode switch
        {
            AddressMode.Wrap => MTLSamplerAddressMode.Repeat,
            AddressMode.Mirror => MTLSamplerAddressMode.MirrorRepeat,
            AddressMode.Clamp => MTLSamplerAddressMode.ClampToEdge,
            AddressMode.Border => MTLSamplerAddressMode.ClampToBorderColor,
            _ => MTLSamplerAddressMode.ClampToEdge
        };
    }

    public static MTLCompareFunction Metal(ComparisonFunc comparisonFunc)
    {
        return comparisonFunc switch
        {
            ComparisonFunc.Never => MTLCompareFunction.Never,
            ComparisonFunc.Less => MTLCompareFunction.Less,
            ComparisonFunc.Equal => MTLCompareFunction.Equal,
            ComparisonFunc.LessEqual => MTLCompareFunction.LessEqual,
            ComparisonFunc.Greater => MTLCompareFunction.Greater,
            ComparisonFunc.NotEqual => MTLCompareFunction.NotEqual,
            ComparisonFunc.GreaterEqual => MTLCompareFunction.GreaterEqual,
            ComparisonFunc.Always => MTLCompareFunction.Always,
            _ => MTLCompareFunction.Never
        };
    }

    public static MTLSamplerBorderColor Metal(BorderColor borderColor)
    {
        return borderColor switch
        {
            BorderColor.TransparentBlack => MTLSamplerBorderColor.TransparentBlack,
            BorderColor.OpaqueBlack => MTLSamplerBorderColor.OpaqueBlack,
            BorderColor.OpaqueWhite => MTLSamplerBorderColor.OpaqueWhite,
            _ => MTLSamplerBorderColor.TransparentBlack
        };
    }

    public static MTLPrimitiveTopologyClass Metal(PrimitiveTopology primitiveTopology)
    {
        throw new NotImplementedException();
    }

    public static MTLStencilOperation Metal(StencilOp stencilOp)
    {
        throw new NotImplementedException();
    }

    public static MTLBlendFactor Metal(Blend blend)
    {
        throw new NotImplementedException();
    }

    public static MTLBlendOperation Metal(BlendOp blendOp)
    {
        throw new NotImplementedException();
    }

    public static MTLColorWriteMask Metal(ColorComponentFlags colorComponentFlags)
    {
        throw new NotImplementedException();
    }

    public static MTLVertexFormat Metal(ElementFormat elementFormat)
    {
        throw new NotImplementedException();
    }
}
