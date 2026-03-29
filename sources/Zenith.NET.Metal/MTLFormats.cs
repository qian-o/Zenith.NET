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

    public static (MTLPixelFormat PixelFormat, MTLAttributeFormat AttributeFormat) Metal(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.R8UNorm => (MTLPixelFormat.R8Unorm, MTLAttributeFormat.UCharNormalized),
            PixelFormat.R8SNorm => (MTLPixelFormat.R8Snorm, MTLAttributeFormat.CharNormalized),
            PixelFormat.R8UInt => (MTLPixelFormat.R8Uint, MTLAttributeFormat.UChar),
            PixelFormat.R8SInt => (MTLPixelFormat.R8Sint, MTLAttributeFormat.Char),

            PixelFormat.R16UNorm => (MTLPixelFormat.R16Unorm, MTLAttributeFormat.UShortNormalized),
            PixelFormat.R16SNorm => (MTLPixelFormat.R16Snorm, MTLAttributeFormat.ShortNormalized),
            PixelFormat.R16UInt => (MTLPixelFormat.R16Uint, MTLAttributeFormat.UShort),
            PixelFormat.R16SInt => (MTLPixelFormat.R16Sint, MTLAttributeFormat.Short),
            PixelFormat.R16Float => (MTLPixelFormat.R16Float, MTLAttributeFormat.Half),

            PixelFormat.R32UInt => (MTLPixelFormat.R32Uint, MTLAttributeFormat.UInt),
            PixelFormat.R32SInt => (MTLPixelFormat.R32Sint, MTLAttributeFormat.Int),
            PixelFormat.R32Float => (MTLPixelFormat.R32Float, MTLAttributeFormat.Float),

            PixelFormat.R8G8UNorm => (MTLPixelFormat.RG8Unorm, MTLAttributeFormat.UChar2Normalized),
            PixelFormat.R8G8SNorm => (MTLPixelFormat.RG8Snorm, MTLAttributeFormat.Char2Normalized),
            PixelFormat.R8G8UInt => (MTLPixelFormat.RG8Uint, MTLAttributeFormat.UChar2),
            PixelFormat.R8G8SInt => (MTLPixelFormat.RG8Sint, MTLAttributeFormat.Char2),

            PixelFormat.R16G16UNorm => (MTLPixelFormat.RG16Unorm, MTLAttributeFormat.UShort2Normalized),
            PixelFormat.R16G16SNorm => (MTLPixelFormat.RG16Snorm, MTLAttributeFormat.Short2Normalized),
            PixelFormat.R16G16UInt => (MTLPixelFormat.RG16Uint, MTLAttributeFormat.UShort2),
            PixelFormat.R16G16SInt => (MTLPixelFormat.RG16Sint, MTLAttributeFormat.Short2),
            PixelFormat.R16G16Float => (MTLPixelFormat.RG16Float, MTLAttributeFormat.Half2),

            PixelFormat.R32G32UInt => (MTLPixelFormat.RG32Uint, MTLAttributeFormat.UInt2),
            PixelFormat.R32G32SInt => (MTLPixelFormat.RG32Sint, MTLAttributeFormat.Int2),
            PixelFormat.R32G32Float => (MTLPixelFormat.RG32Float, MTLAttributeFormat.Float2),

            PixelFormat.R32G32B32UInt => (MTLPixelFormat.Invalid, MTLAttributeFormat.UInt3),
            PixelFormat.R32G32B32SInt => (MTLPixelFormat.Invalid, MTLAttributeFormat.Int3),
            PixelFormat.R32G32B32Float => (MTLPixelFormat.Invalid, MTLAttributeFormat.Float3),

            PixelFormat.R8G8B8A8UNorm => (MTLPixelFormat.RGBA8Unorm, MTLAttributeFormat.UChar4Normalized),
            PixelFormat.R8G8B8A8SNorm => (MTLPixelFormat.RGBA8Snorm, MTLAttributeFormat.Char4Normalized),
            PixelFormat.R8G8B8A8UInt => (MTLPixelFormat.RGBA8Uint, MTLAttributeFormat.UChar4),
            PixelFormat.R8G8B8A8SInt => (MTLPixelFormat.RGBA8Sint, MTLAttributeFormat.Char4),
            PixelFormat.R8G8B8A8SRgb => (MTLPixelFormat.RGBA8Unorm_sRGB, MTLAttributeFormat.UChar4Normalized),

            PixelFormat.R16G16B16A16UNorm => (MTLPixelFormat.RGBA16Unorm, MTLAttributeFormat.UShort4Normalized),
            PixelFormat.R16G16B16A16SNorm => (MTLPixelFormat.RGBA16Snorm, MTLAttributeFormat.Short4Normalized),
            PixelFormat.R16G16B16A16UInt => (MTLPixelFormat.RGBA16Uint, MTLAttributeFormat.UShort4),
            PixelFormat.R16G16B16A16SInt => (MTLPixelFormat.RGBA16Sint, MTLAttributeFormat.Short4),
            PixelFormat.R16G16B16A16Float => (MTLPixelFormat.RGBA16Float, MTLAttributeFormat.Half4),

            PixelFormat.R32G32B32A32UInt => (MTLPixelFormat.RGBA32Uint, MTLAttributeFormat.UInt4),
            PixelFormat.R32G32B32A32SInt => (MTLPixelFormat.RGBA32Sint, MTLAttributeFormat.Int4),
            PixelFormat.R32G32B32A32Float => (MTLPixelFormat.RGBA32Float, MTLAttributeFormat.Float4),

            PixelFormat.B8G8R8A8UNorm => (MTLPixelFormat.BGRA8Unorm, MTLAttributeFormat.UChar4Normalized_BGRA),
            PixelFormat.B8G8R8A8SRgb => (MTLPixelFormat.BGRA8Unorm_sRGB, MTLAttributeFormat.UChar4Normalized_BGRA),

            PixelFormat.D16UNorm => (MTLPixelFormat.Depth16Unorm, MTLAttributeFormat.Invalid),
            PixelFormat.D24UNormS8UInt => (MTLPixelFormat.Depth24Unorm_Stencil8, MTLAttributeFormat.Invalid),
            PixelFormat.D32Float => (MTLPixelFormat.Depth32Float, MTLAttributeFormat.Invalid),
            PixelFormat.D32FloatS8UInt => (MTLPixelFormat.Depth32Float_Stencil8, MTLAttributeFormat.Invalid),

            PixelFormat.BC4UNorm => (MTLPixelFormat.BC4_RUnorm, MTLAttributeFormat.Invalid),
            PixelFormat.BC4SNorm => (MTLPixelFormat.BC4_RSnorm, MTLAttributeFormat.Invalid),

            PixelFormat.BC5UNorm => (MTLPixelFormat.BC5_RGUnorm, MTLAttributeFormat.Invalid),
            PixelFormat.BC5SNorm => (MTLPixelFormat.BC5_RGSnorm, MTLAttributeFormat.Invalid),

            PixelFormat.BC6HUFloat => (MTLPixelFormat.BC6H_RGBUfloat, MTLAttributeFormat.Invalid),
            PixelFormat.BC6HSFloat => (MTLPixelFormat.BC6H_RGBFloat, MTLAttributeFormat.Invalid),

            PixelFormat.BC7UNorm => (MTLPixelFormat.BC7_RGBAUnorm, MTLAttributeFormat.Invalid),
            PixelFormat.BC7SRgb => (MTLPixelFormat.BC7_RGBAUnorm_sRGB, MTLAttributeFormat.Invalid),

            PixelFormat.ETC2UNorm => (MTLPixelFormat.ETC2_RGB8, MTLAttributeFormat.Invalid),
            PixelFormat.ETC2SRgb => (MTLPixelFormat.ETC2_RGB8_sRGB, MTLAttributeFormat.Invalid),

            PixelFormat.ETC2A1UNorm => (MTLPixelFormat.ETC2_RGB8A1, MTLAttributeFormat.Invalid),
            PixelFormat.ETC2A1SRgb => (MTLPixelFormat.ETC2_RGB8A1_sRGB, MTLAttributeFormat.Invalid),

            PixelFormat.ETC2A8UNorm => (MTLPixelFormat.EAC_RGBA8, MTLAttributeFormat.Invalid),
            PixelFormat.ETC2A8SRgb => (MTLPixelFormat.EAC_RGBA8_sRGB, MTLAttributeFormat.Invalid),

            PixelFormat.ASTC4x4UNorm => (MTLPixelFormat.ASTC_4x4_LDR, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC4x4SRgb => (MTLPixelFormat.ASTC_4x4_sRGB, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC4x4Float => (MTLPixelFormat.ASTC_4x4_HDR, MTLAttributeFormat.Invalid),

            PixelFormat.ASTC5x5UNorm => (MTLPixelFormat.ASTC_5x5_LDR, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC5x5SRgb => (MTLPixelFormat.ASTC_5x5_sRGB, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC5x5Float => (MTLPixelFormat.ASTC_5x5_HDR, MTLAttributeFormat.Invalid),

            PixelFormat.ASTC6x6UNorm => (MTLPixelFormat.ASTC_6x6_LDR, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC6x6SRgb => (MTLPixelFormat.ASTC_6x6_sRGB, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC6x6Float => (MTLPixelFormat.ASTC_6x6_HDR, MTLAttributeFormat.Invalid),

            PixelFormat.ASTC8x8UNorm => (MTLPixelFormat.ASTC_8x8_LDR, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC8x8SRgb => (MTLPixelFormat.ASTC_8x8_sRGB, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC8x8Float => (MTLPixelFormat.ASTC_8x8_HDR, MTLAttributeFormat.Invalid),

            PixelFormat.ASTC10x10UNorm => (MTLPixelFormat.ASTC_10x10_LDR, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC10x10SRgb => (MTLPixelFormat.ASTC_10x10_sRGB, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC10x10Float => (MTLPixelFormat.ASTC_10x10_HDR, MTLAttributeFormat.Invalid),

            PixelFormat.ASTC12x12UNorm => (MTLPixelFormat.ASTC_12x12_LDR, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC12x12SRgb => (MTLPixelFormat.ASTC_12x12_sRGB, MTLAttributeFormat.Invalid),
            PixelFormat.ASTC12x12Float => (MTLPixelFormat.ASTC_12x12_HDR, MTLAttributeFormat.Invalid),

            _ => (MTLPixelFormat.Invalid, MTLAttributeFormat.Invalid)
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

    public static (MTLPrimitiveTopologyClass TopologyClass, MTLPrimitiveType Type) Metal(PrimitiveTopology primitiveTopology)
    {
        return
        (
            primitiveTopology switch
            {
                PrimitiveTopology.PointList => MTLPrimitiveTopologyClass.Point,

                PrimitiveTopology.LineList or
                PrimitiveTopology.LineStrip => MTLPrimitiveTopologyClass.Line,

                PrimitiveTopology.TriangleList or
                PrimitiveTopology.TriangleStrip => MTLPrimitiveTopologyClass.Triangle,

                _ => MTLPrimitiveTopologyClass.Unspecified
            },
            primitiveTopology switch
            {
                PrimitiveTopology.PointList => MTLPrimitiveType.Point,
                PrimitiveTopology.LineList => MTLPrimitiveType.Line,
                PrimitiveTopology.LineStrip => MTLPrimitiveType.LineStrip,
                PrimitiveTopology.TriangleList => MTLPrimitiveType.Triangle,
                PrimitiveTopology.TriangleStrip => MTLPrimitiveType.TriangleStrip,
                _ => MTLPrimitiveType.Point
            }
        );
    }

    public static MTLStencilOperation Metal(StencilOp stencilOp)
    {
        return stencilOp switch
        {
            StencilOp.Keep => MTLStencilOperation.Keep,
            StencilOp.Zero => MTLStencilOperation.Zero,
            StencilOp.Replace => MTLStencilOperation.Replace,
            StencilOp.IncrementAndClamp => MTLStencilOperation.IncrementClamp,
            StencilOp.DecrementAndClamp => MTLStencilOperation.DecrementClamp,
            StencilOp.Invert => MTLStencilOperation.Invert,
            StencilOp.IncrementAndWrap => MTLStencilOperation.IncrementWrap,
            StencilOp.DecrementAndWrap => MTLStencilOperation.DecrementWrap,
            _ => MTLStencilOperation.Keep
        };
    }

    public static MTLBlendFactor Metal(Blend blend)
    {
        return blend switch
        {
            Blend.Zero => MTLBlendFactor.Zero,
            Blend.One => MTLBlendFactor.One,
            Blend.SrcAlpha => MTLBlendFactor.SourceAlpha,
            Blend.InverseSrcAlpha => MTLBlendFactor.OneMinusSourceAlpha,
            Blend.DestAlpha => MTLBlendFactor.DestinationAlpha,
            Blend.InverseDestAlpha => MTLBlendFactor.OneMinusDestinationAlpha,
            Blend.SrcColor => MTLBlendFactor.SourceColor,
            Blend.InverseSrcColor => MTLBlendFactor.OneMinusSourceColor,
            Blend.DestColor => MTLBlendFactor.DestinationColor,
            Blend.InverseDestColor => MTLBlendFactor.OneMinusDestinationColor,
            Blend.BlendFactor => MTLBlendFactor.BlendColor,
            Blend.InverseBlendFactor => MTLBlendFactor.OneMinusBlendColor,
            _ => MTLBlendFactor.Zero
        };
    }

    public static MTLBlendOperation Metal(BlendOp blendOp)
    {
        return blendOp switch
        {
            BlendOp.Add => MTLBlendOperation.Add,
            BlendOp.Subtract => MTLBlendOperation.Subtract,
            BlendOp.ReverseSubtract => MTLBlendOperation.ReverseSubtract,
            BlendOp.Min => MTLBlendOperation.Min,
            BlendOp.Max => MTLBlendOperation.Max,
            _ => MTLBlendOperation.Add
        };
    }

    public static MTLColorWriteMask Metal(ColorComponentFlags colorComponentFlags)
    {
        MTLColorWriteMask result = MTLColorWriteMask.None;

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Red))
        {
            result |= MTLColorWriteMask.Red;
        }

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Green))
        {
            result |= MTLColorWriteMask.Green;
        }

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Blue))
        {
            result |= MTLColorWriteMask.Blue;
        }

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Alpha))
        {
            result |= MTLColorWriteMask.Alpha;
        }

        return result;
    }

    public static MTLVertexFormat Metal(ElementFormat elementFormat)
    {
        return elementFormat switch
        {
            ElementFormat.UByte1 => MTLVertexFormat.UChar,
            ElementFormat.UByte2 => MTLVertexFormat.UChar2,
            ElementFormat.UByte4 => MTLVertexFormat.UChar4,
            ElementFormat.Byte1 => MTLVertexFormat.Char,
            ElementFormat.Byte2 => MTLVertexFormat.Char2,
            ElementFormat.Byte4 => MTLVertexFormat.Char4,

            ElementFormat.UByte1Normalized => MTLVertexFormat.UCharNormalized,
            ElementFormat.UByte2Normalized => MTLVertexFormat.UChar2Normalized,
            ElementFormat.UByte4Normalized => MTLVertexFormat.UChar4Normalized,
            ElementFormat.Byte1Normalized => MTLVertexFormat.CharNormalized,
            ElementFormat.Byte2Normalized => MTLVertexFormat.Char2Normalized,
            ElementFormat.Byte4Normalized => MTLVertexFormat.Char4Normalized,

            ElementFormat.UShort1 => MTLVertexFormat.UShort,
            ElementFormat.UShort2 => MTLVertexFormat.UShort2,
            ElementFormat.UShort4 => MTLVertexFormat.UShort4,
            ElementFormat.Short1 => MTLVertexFormat.Short,
            ElementFormat.Short2 => MTLVertexFormat.Short2,
            ElementFormat.Short4 => MTLVertexFormat.Short4,

            ElementFormat.UShort1Normalized => MTLVertexFormat.UShortNormalized,
            ElementFormat.UShort2Normalized => MTLVertexFormat.UShort2Normalized,
            ElementFormat.UShort4Normalized => MTLVertexFormat.UShort4Normalized,
            ElementFormat.Short1Normalized => MTLVertexFormat.ShortNormalized,
            ElementFormat.Short2Normalized => MTLVertexFormat.Short2Normalized,
            ElementFormat.Short4Normalized => MTLVertexFormat.Short4Normalized,

            ElementFormat.Half1 => MTLVertexFormat.Half,
            ElementFormat.Half2 => MTLVertexFormat.Half2,
            ElementFormat.Half4 => MTLVertexFormat.Half4,

            ElementFormat.Float1 => MTLVertexFormat.Float,
            ElementFormat.Float2 => MTLVertexFormat.Float2,
            ElementFormat.Float3 => MTLVertexFormat.Float3,
            ElementFormat.Float4 => MTLVertexFormat.Float4,

            ElementFormat.UInt1 => MTLVertexFormat.UInt,
            ElementFormat.UInt2 => MTLVertexFormat.UInt2,
            ElementFormat.UInt3 => MTLVertexFormat.UInt3,
            ElementFormat.UInt4 => MTLVertexFormat.UInt4,
            ElementFormat.Int1 => MTLVertexFormat.Int,
            ElementFormat.Int2 => MTLVertexFormat.Int2,
            ElementFormat.Int3 => MTLVertexFormat.Int3,
            ElementFormat.Int4 => MTLVertexFormat.Int4,

            _ => MTLVertexFormat.Invalid
        };
    }

    public static MTLCullMode Metal(CullMode cullMode)
    {
        return cullMode switch
        {
            CullMode.None => MTLCullMode.None,
            CullMode.Front => MTLCullMode.Front,
            CullMode.Back => MTLCullMode.Back,
            _ => MTLCullMode.None
        };
    }

    public static MTLTriangleFillMode Metal(FillMode fillMode)
    {
        return fillMode switch
        {
            FillMode.Solid => MTLTriangleFillMode.Fill,
            FillMode.Wireframe => MTLTriangleFillMode.Lines,
            _ => MTLTriangleFillMode.Fill
        };
    }

    public static MTLWinding Metal(FrontFace frontFace)
    {
        return frontFace switch
        {
            FrontFace.CounterClockwise => MTLWinding.CounterClockwise,
            FrontFace.Clockwise => MTLWinding.Clockwise,
            _ => MTLWinding.Clockwise
        };
    }

    public static MTLIndexType Metal(IndexFormat indexFormat)
    {
        return indexFormat switch
        {
            IndexFormat.UInt16 => MTLIndexType.UInt16,
            IndexFormat.UInt32 => MTLIndexType.UInt32,
            _ => MTLIndexType.UInt16
        };
    }

    public static MTLVisibilityResultMode Metal(QueryType queryType)
    {
        return queryType switch
        {
            QueryType.Occlusion => MTLVisibilityResultMode.Counting,
            QueryType.BinaryOcclusion => MTLVisibilityResultMode.Boolean,
            _ => MTLVisibilityResultMode.Disabled
        };
    }

    public static MTLAccelerationStructureUsage Metal(AccelerationStructureBuildFlags accelerationStructureBuildFlags)
    {
        MTLAccelerationStructureUsage result = MTLAccelerationStructureUsage.None;

        if (accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.AllowUpdate) || accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.PerformUpdate))
        {
            result |= MTLAccelerationStructureUsage.Refit;
        }

        if (accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.PreferFastTrace))
        {
            result |= MTLAccelerationStructureUsage.PreferFastIntersection;
        }

        if (accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.PreferFastBuild))
        {
            result |= MTLAccelerationStructureUsage.PreferFastBuild;
        }

        if (accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.MinimizeMemory))
        {
            result |= MTLAccelerationStructureUsage.MinimizeMemory;
        }

        return result;
    }
}
