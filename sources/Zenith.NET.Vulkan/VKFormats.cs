using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal static class VKFormats
{
    public static VkShaderStageFlags Vulkan(ShaderStageFlags shaderStageFlags)
    {
        VkShaderStageFlags result = VkShaderStageFlags.None;

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Vertex))
        {
            result |= VkShaderStageFlags.VertexBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Hull))
        {
            result |= VkShaderStageFlags.TessellationControlBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Domain))
        {
            result |= VkShaderStageFlags.TessellationEvaluationBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Geometry))
        {
            result |= VkShaderStageFlags.GeometryBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Pixel))
        {
            result |= VkShaderStageFlags.FragmentBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Compute))
        {
            result |= VkShaderStageFlags.ComputeBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.RayGeneration))
        {
            result |= VkShaderStageFlags.RaygenBitKhr;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Miss))
        {
            result |= VkShaderStageFlags.MissBitKhr;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.AnyHit))
        {
            result |= VkShaderStageFlags.AnyHitBitKhr;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Intersection))
        {
            result |= VkShaderStageFlags.IntersectionBitKhr;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.ClosestHit))
        {
            result |= VkShaderStageFlags.ClosestHitBitKhr;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Amplification))
        {
            result |= VkShaderStageFlags.TaskBitExt;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Mesh))
        {
            result |= VkShaderStageFlags.MeshBitExt;
        }

        return result;
    }

    public static VkBufferUsageFlags Vulkan(BufferUsageFlags bufferUsageFlags)
    {
        VkBufferUsageFlags result = VkBufferUsageFlags.None;

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.Vertex))
        {
            result |= VkBufferUsageFlags.VertexBufferBit;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.Index))
        {
            result |= VkBufferUsageFlags.IndexBufferBit;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.Indirect))
        {
            result |= VkBufferUsageFlags.IndirectBufferBit;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.AccelerationStructure))
        {
            result |= VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.Constant))
        {
            result |= VkBufferUsageFlags.UniformBufferBit;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.ShaderResource) || bufferUsageFlags.HasFlag(BufferUsageFlags.UnorderedAccess))
        {
            result |= VkBufferUsageFlags.StorageBufferBit;
        }

        return result;
    }

    public static (ImageType ImageType, ImageViewType ImageViewType) Vulkan(TextureType textureType)
    {
        ImageType imageType = textureType switch
        {
            TextureType.Texture1D or
            TextureType.Texture1DArray => ImageType.Type1D,

            TextureType.Texture2D or
            TextureType.Texture2DArray => ImageType.Type2D,

            TextureType.Texture3D or
            TextureType.TextureCube or
            TextureType.TextureCubeArray => ImageType.Type3D,

            _ => ImageType.Type1D
        };

        ImageViewType imageViewType = textureType switch
        {
            TextureType.Texture1D => ImageViewType.Type1D,
            TextureType.Texture1DArray => ImageViewType.Type1DArray,
            TextureType.Texture2D => ImageViewType.Type2D,
            TextureType.Texture2DArray => ImageViewType.Type2DArray,
            TextureType.Texture3D => ImageViewType.Type3D,
            TextureType.TextureCube => ImageViewType.TypeCube,
            TextureType.TextureCubeArray => ImageViewType.TypeCubeArray,
            _ => ImageViewType.Type1D
        };

        return (imageType, imageViewType);
    }

    public static Format Vulkan(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.R8UNorm => Format.R8Unorm,
            PixelFormat.R8SNorm => Format.R8SNorm,
            PixelFormat.R8UInt => Format.R8Uint,
            PixelFormat.R8SInt => Format.R8Sint,

            PixelFormat.R16UNorm => Format.R16Unorm,
            PixelFormat.R16SNorm => Format.R16SNorm,
            PixelFormat.R16UInt => Format.R16Uint,
            PixelFormat.R16SInt => Format.R16Sint,
            PixelFormat.R16Float => Format.R16Sfloat,

            PixelFormat.R32UInt => Format.R32Uint,
            PixelFormat.R32SInt => Format.R32Sint,
            PixelFormat.R32Float => Format.R32Sfloat,

            PixelFormat.R8G8UNorm => Format.R8G8Unorm,
            PixelFormat.R8G8SNorm => Format.R8G8SNorm,
            PixelFormat.R8G8UInt => Format.R8G8Uint,
            PixelFormat.R8G8SInt => Format.R8G8Sint,

            PixelFormat.R16G16UNorm => Format.R16G16Unorm,
            PixelFormat.R16G16SNorm => Format.R16G16SNorm,
            PixelFormat.R16G16UInt => Format.R16G16Uint,
            PixelFormat.R16G16SInt => Format.R16G16Sint,
            PixelFormat.R16G16Float => Format.R16G16Sfloat,

            PixelFormat.R32G32UInt => Format.R32G32Uint,
            PixelFormat.R32G32SInt => Format.R32G32Sint,
            PixelFormat.R32G32Float => Format.R32G32Sfloat,

            PixelFormat.R32G32B32UInt => Format.R32G32B32Uint,
            PixelFormat.R32G32B32SInt => Format.R32G32B32Sint,
            PixelFormat.R32G32B32Float => Format.R32G32B32Sfloat,

            PixelFormat.R8G8B8A8UNorm => Format.R8G8B8A8Unorm,
            PixelFormat.R8G8B8A8UNormSRgb => Format.R8G8B8A8Srgb,
            PixelFormat.R8G8B8A8SNorm => Format.R8G8B8A8SNorm,
            PixelFormat.R8G8B8A8UInt => Format.R8G8B8A8Uint,
            PixelFormat.R8G8B8A8SInt => Format.R8G8B8A8Sint,

            PixelFormat.R16G16B16A16UNorm => Format.R16G16B16A16Unorm,
            PixelFormat.R16G16B16A16SNorm => Format.R16G16B16A16SNorm,
            PixelFormat.R16G16B16A16UInt => Format.R16G16B16A16Uint,
            PixelFormat.R16G16B16A16SInt => Format.R16G16B16A16Sint,
            PixelFormat.R16G16B16A16Float => Format.R16G16B16A16Sfloat,

            PixelFormat.R32G32B32A32UInt => Format.R32G32B32A32Uint,
            PixelFormat.R32G32B32A32SInt => Format.R32G32B32A32Sint,
            PixelFormat.R32G32B32A32Float => Format.R32G32B32A32Sfloat,

            PixelFormat.B8G8R8A8UNorm => Format.B8G8R8A8Unorm,
            PixelFormat.B8G8R8A8UNormSRgb => Format.B8G8R8A8Srgb,

            PixelFormat.D24UNormS8UInt => Format.D24UnormS8Uint,
            PixelFormat.D32FloatS8UInt => Format.D32SfloatS8Uint,

            PixelFormat.BC1UNorm => Format.BC1RgbaUnormBlock,
            PixelFormat.BC1UNormSRgb => Format.BC1RgbaSrgbBlock,

            PixelFormat.BC2UNorm => Format.BC2UnormBlock,
            PixelFormat.BC2UNormSRgb => Format.BC2SrgbBlock,

            PixelFormat.BC3UNorm => Format.BC3UnormBlock,
            PixelFormat.BC3UNormSRgb => Format.BC3SrgbBlock,

            PixelFormat.BC4UNorm => Format.BC4UnormBlock,
            PixelFormat.BC4SNorm => Format.BC4SNormBlock,

            PixelFormat.BC5UNorm => Format.BC5UnormBlock,
            PixelFormat.BC5SNorm => Format.BC5SNormBlock,

            PixelFormat.BC7UNorm => Format.BC7UnormBlock,
            PixelFormat.BC7UNormSRgb => Format.BC7SrgbBlock,

            _ => Format.Undefined
        };
    }

    public static SampleCountFlags Vulkan(SampleCount sampleCount)
    {
        return sampleCount switch
        {
            SampleCount.Count1 => SampleCountFlags.Count1Bit,
            SampleCount.Count2 => SampleCountFlags.Count2Bit,
            SampleCount.Count4 => SampleCountFlags.Count4Bit,
            SampleCount.Count8 => SampleCountFlags.Count8Bit,
            SampleCount.Count16 => SampleCountFlags.Count16Bit,
            SampleCount.Count32 => SampleCountFlags.Count32Bit,
            _ => SampleCountFlags.None
        };
    }

    public static (ImageUsageFlags ImageUsageFlags, ImageAspectFlags ImageAspectFlags) Vulkan(TextureUsageFlags textureUsageFlags)
    {
        ImageUsageFlags imageUsageFlags = ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit;

        if (textureUsageFlags.HasFlag(TextureUsageFlags.RenderTarget))
        {
            imageUsageFlags |= ImageUsageFlags.ColorAttachmentBit;
        }

        if (textureUsageFlags.HasFlag(TextureUsageFlags.DepthStencil))
        {
            imageUsageFlags |= ImageUsageFlags.DepthStencilAttachmentBit;
        }

        if (textureUsageFlags.HasFlag(TextureUsageFlags.ShaderResource) || textureUsageFlags.HasFlag(TextureUsageFlags.Dynamic))
        {
            imageUsageFlags |= ImageUsageFlags.SampledBit;
        }

        if (textureUsageFlags.HasFlag(TextureUsageFlags.UnorderedAccess))
        {
            imageUsageFlags |= ImageUsageFlags.StorageBit;
        }

        ImageAspectFlags imageAspectFlags = ImageAspectFlags.None;

        if (textureUsageFlags.HasFlag(TextureUsageFlags.DepthStencil))
        {
            imageAspectFlags |= ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit;
        }
        else
        {
            imageAspectFlags |= ImageAspectFlags.ColorBit;
        }

        return (imageUsageFlags, imageAspectFlags);
    }

    public static (VkFilter MinFilter, VkFilter MagFilter, SamplerMipmapMode MipmapMode) Vulkan(Filter filter)
    {
        VkFilter minFilter = VkFilter.Nearest;
        VkFilter magFilter = VkFilter.Nearest;
        SamplerMipmapMode mipmapMode = SamplerMipmapMode.Nearest;

        switch (filter)
        {
            case Filter.MinPointMagPointMipPoint:
                minFilter = VkFilter.Nearest;
                magFilter = VkFilter.Nearest;
                mipmapMode = SamplerMipmapMode.Nearest;
                break;

            case Filter.MinPointMagPointMipLinear:
                minFilter = VkFilter.Nearest;
                magFilter = VkFilter.Nearest;
                mipmapMode = SamplerMipmapMode.Linear;
                break;

            case Filter.MinPointMagLinearMipPoint:
                minFilter = VkFilter.Nearest;
                magFilter = VkFilter.Linear;
                mipmapMode = SamplerMipmapMode.Nearest;
                break;

            case Filter.MinPointMagLinearMipLinear:
                minFilter = VkFilter.Nearest;
                magFilter = VkFilter.Linear;
                mipmapMode = SamplerMipmapMode.Linear;
                break;

            case Filter.MinLinearMagPointMipPoint:
                minFilter = VkFilter.Linear;
                magFilter = VkFilter.Nearest;
                mipmapMode = SamplerMipmapMode.Nearest;
                break;

            case Filter.MinLinearMagPointMipLinear:
                minFilter = VkFilter.Linear;
                magFilter = VkFilter.Nearest;
                mipmapMode = SamplerMipmapMode.Linear;
                break;

            case Filter.MinLinearMagLinearMipPoint:
                minFilter = VkFilter.Linear;
                magFilter = VkFilter.Linear;
                mipmapMode = SamplerMipmapMode.Nearest;
                break;

            case Filter.MinLinearMagLinearMipLinear:
                minFilter = VkFilter.Linear;
                magFilter = VkFilter.Linear;
                mipmapMode = SamplerMipmapMode.Linear;
                break;

            case Filter.Anisotropic:
                minFilter = VkFilter.Linear;
                magFilter = VkFilter.Linear;
                mipmapMode = SamplerMipmapMode.Linear;
                break;
        }

        return (minFilter, magFilter, mipmapMode);
    }

    public static SamplerAddressMode Vulkan(AddressMode addressMode)
    {
        return addressMode switch
        {
            AddressMode.Wrap => SamplerAddressMode.Repeat,
            AddressMode.Mirror => SamplerAddressMode.MirroredRepeat,
            AddressMode.Clamp => SamplerAddressMode.ClampToEdge,
            AddressMode.Border => SamplerAddressMode.ClampToBorder,
            _ => SamplerAddressMode.Repeat
        };
    }

    public static CompareOp Vulkan(ComparisonFunc comparisonFunc)
    {
        return comparisonFunc switch
        {
            ComparisonFunc.Never => CompareOp.Never,
            ComparisonFunc.Less => CompareOp.Less,
            ComparisonFunc.Equal => CompareOp.Equal,
            ComparisonFunc.LessEqual => CompareOp.LessOrEqual,
            ComparisonFunc.Greater => CompareOp.Greater,
            ComparisonFunc.NotEqual => CompareOp.NotEqual,
            ComparisonFunc.GreaterEqual => CompareOp.GreaterOrEqual,
            ComparisonFunc.Always => CompareOp.Always,
            _ => CompareOp.Never
        };
    }

    public static VkBorderColor Vulkan(BorderColor borderColor)
    {
        return borderColor switch
        {
            BorderColor.TransparentBlack => VkBorderColor.FloatTransparentBlack,
            BorderColor.OpaqueBlack => VkBorderColor.FloatOpaqueBlack,
            BorderColor.OpaqueWhite => VkBorderColor.FloatOpaqueWhite,
            _ => VkBorderColor.FloatTransparentBlack
        };
    }

    public static DescriptorType Vulkan(ResourceType type)
    {
        return type switch
        {
            ResourceType.ConstantBuffer => DescriptorType.UniformBuffer,

            ResourceType.StructuredBuffer or
            ResourceType.StructuredBufferReadWrite => DescriptorType.StorageBuffer,

            ResourceType.Texture => DescriptorType.SampledImage,

            ResourceType.TextureReadWrite => DescriptorType.StorageImage,

            ResourceType.Sampler => DescriptorType.Sampler,

            ResourceType.AccelerationStructure => DescriptorType.AccelerationStructureKhr,

            _ => DescriptorType.Sampler
        };
    }

    public static PolygonMode Vulkan(FillMode fillMode)
    {
        return fillMode switch
        {
            FillMode.Solid => PolygonMode.Fill,
            FillMode.Wireframe => PolygonMode.Line,
            _ => PolygonMode.Fill
        };
    }

    public static CullModeFlags Vulkan(CullMode cullMode)
    {
        return cullMode switch
        {
            CullMode.None => CullModeFlags.None,
            CullMode.Front => CullModeFlags.FrontBit,
            CullMode.Back => CullModeFlags.BackBit,
            _ => CullModeFlags.None
        };
    }

    public static VkFrontFace Vulkan(FrontFace frontFace)
    {
        return frontFace switch
        {
            FrontFace.CounterClockwise => VkFrontFace.CounterClockwise,
            FrontFace.Clockwise => VkFrontFace.Clockwise,
            _ => VkFrontFace.CounterClockwise
        };
    }

    public static VkStencilOp Vulkan(StencilOp stencilOp)
    {
        return stencilOp switch
        {
            StencilOp.Keep => VkStencilOp.Keep,
            StencilOp.Zero => VkStencilOp.Zero,
            StencilOp.Replace => VkStencilOp.Replace,
            StencilOp.IncrementAndClamp => VkStencilOp.IncrementAndClamp,
            StencilOp.DecrementAndClamp => VkStencilOp.DecrementAndClamp,
            StencilOp.Invert => VkStencilOp.Invert,
            StencilOp.IncrementAndWrap => VkStencilOp.IncrementAndWrap,
            StencilOp.DecrementAndWrap => VkStencilOp.DecrementAndWrap,
            _ => VkStencilOp.Keep
        };
    }

    public static BlendFactor Vulkan(Blend blend)
    {
        return blend switch
        {
            Blend.Zero => BlendFactor.Zero,
            Blend.One => BlendFactor.One,
            Blend.SrcAlpha => BlendFactor.SrcAlpha,
            Blend.InverseSrcAlpha => BlendFactor.OneMinusSrcAlpha,
            Blend.DestAlpha => BlendFactor.DstAlpha,
            Blend.InverseDestAlpha => BlendFactor.OneMinusDstAlpha,
            Blend.SrcColor => BlendFactor.SrcColor,
            Blend.InverseSrcColor => BlendFactor.OneMinusSrcColor,
            Blend.DestColor => BlendFactor.DstColor,
            Blend.InverseDestColor => BlendFactor.OneMinusDstColor,
            Blend.BlendFactor => BlendFactor.ConstantColor,
            Blend.InverseBlendFactor => BlendFactor.OneMinusConstantColor,
            _ => BlendFactor.Zero
        };
    }

    public static VkBlendOp Vulkan(BlendOp blendOp)
    {
        return blendOp switch
        {
            BlendOp.Add => VkBlendOp.Add,
            BlendOp.Subtract => VkBlendOp.Subtract,
            BlendOp.ReverseSubtract => VkBlendOp.ReverseSubtract,
            BlendOp.Min => VkBlendOp.Min,
            BlendOp.Max => VkBlendOp.Max,
            _ => VkBlendOp.Add
        };
    }

    public static VkColorComponentFlags Vulkan(ColorComponentFlags colorComponentFlags)
    {
        VkColorComponentFlags result = VkColorComponentFlags.None;

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Red))
        {
            result |= VkColorComponentFlags.RBit;
        }

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Green))
        {
            result |= VkColorComponentFlags.GBit;
        }

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Blue))
        {
            result |= VkColorComponentFlags.BBit;
        }

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Alpha))
        {
            result |= VkColorComponentFlags.ABit;
        }

        return result;
    }

    public static Format Vulkan(ElementFormat elementFormat)
    {
        return elementFormat switch
        {
            ElementFormat.UByte1 => Format.R8Uint,
            ElementFormat.UByte2 => Format.R8G8Uint,
            ElementFormat.UByte4 => Format.R8G8B8A8Uint,
            ElementFormat.Byte1 => Format.R8Sint,
            ElementFormat.Byte2 => Format.R8G8Sint,
            ElementFormat.Byte4 => Format.R8G8B8A8Sint,

            ElementFormat.UByte1Normalized => Format.R8Unorm,
            ElementFormat.UByte2Normalized => Format.R8G8Unorm,
            ElementFormat.UByte4Normalized => Format.R8G8B8A8Unorm,
            ElementFormat.Byte1Normalized => Format.R8SNorm,
            ElementFormat.Byte2Normalized => Format.R8G8SNorm,
            ElementFormat.Byte4Normalized => Format.R8G8B8A8SNorm,

            ElementFormat.UShort1 => Format.R16Uint,
            ElementFormat.UShort2 => Format.R16G16Uint,
            ElementFormat.UShort4 => Format.R16G16B16A16Uint,
            ElementFormat.Short1 => Format.R16Sint,
            ElementFormat.Short2 => Format.R16G16Sint,
            ElementFormat.Short4 => Format.R16G16B16A16Sint,

            ElementFormat.UShort1Normalized => Format.R16Unorm,
            ElementFormat.UShort2Normalized => Format.R16G16Unorm,
            ElementFormat.UShort4Normalized => Format.R16G16B16A16Unorm,
            ElementFormat.Short1Normalized => Format.R16SNorm,
            ElementFormat.Short2Normalized => Format.R16G16SNorm,
            ElementFormat.Short4Normalized => Format.R16G16B16A16SNorm,

            ElementFormat.Half1 => Format.R16Sfloat,
            ElementFormat.Half2 => Format.R16G16Sfloat,
            ElementFormat.Half4 => Format.R16G16B16A16Sfloat,

            ElementFormat.Float1 => Format.R32Sfloat,
            ElementFormat.Float2 => Format.R32G32Sfloat,
            ElementFormat.Float3 => Format.R32G32B32Sfloat,
            ElementFormat.Float4 => Format.R32G32B32A32Sfloat,

            ElementFormat.UInt1 => Format.R32Uint,
            ElementFormat.UInt2 => Format.R32G32Uint,
            ElementFormat.UInt3 => Format.R32G32B32Uint,
            ElementFormat.UInt4 => Format.R32G32B32A32Uint,
            ElementFormat.Int1 => Format.R32Sint,
            ElementFormat.Int2 => Format.R32G32Sint,
            ElementFormat.Int3 => Format.R32G32B32Sint,
            ElementFormat.Int4 => Format.R32G32B32A32Sint,

            _ => Format.Undefined
        };
    }

    public static VkPrimitiveTopology Vulkan(PrimitiveTopology primitiveTopology)
    {
        return primitiveTopology switch
        {
            PrimitiveTopology.PointList => VkPrimitiveTopology.PointList,
            PrimitiveTopology.LineList => VkPrimitiveTopology.LineList,
            PrimitiveTopology.LineStrip => VkPrimitiveTopology.LineStrip,
            PrimitiveTopology.TriangleList => VkPrimitiveTopology.TriangleList,
            PrimitiveTopology.TriangleStrip => VkPrimitiveTopology.TriangleStrip,
            PrimitiveTopology.LineListWithAdjacency => VkPrimitiveTopology.LineListWithAdjacency,
            PrimitiveTopology.LineStripWithAdjacency => VkPrimitiveTopology.LineStripWithAdjacency,
            PrimitiveTopology.TriangleListWithAdjacency => VkPrimitiveTopology.TriangleListWithAdjacency,
            PrimitiveTopology.TriangleStripWithAdjacency => VkPrimitiveTopology.TriangleStripWithAdjacency,
            PrimitiveTopology.PatchList => VkPrimitiveTopology.PatchList,
            _ => VkPrimitiveTopology.PointList
        };
    }

    public static RayTracingShaderGroupTypeKHR Vulkan(HitGroupType hitGroupType)
    {
        return hitGroupType switch
        {
            HitGroupType.Triangles => RayTracingShaderGroupTypeKHR.TrianglesHitGroupKhr,
            HitGroupType.Procedural => RayTracingShaderGroupTypeKHR.ProceduralHitGroupKhr,
            _ => RayTracingShaderGroupTypeKHR.GeneralKhr
        };
    }

    public static VkQueryType Vulkan(QueryType queryType)
    {
        return queryType switch
        {
            QueryType.Occlusion or QueryType.BinaryOcclusion => VkQueryType.Occlusion,
            QueryType.Timestamp => VkQueryType.Timestamp,
            _ => VkQueryType.Occlusion
        };
    }

    public static IndexType Vulkan(IndexFormat indexFormat)
    {
        return indexFormat switch
        {
            IndexFormat.UInt16 => IndexType.Uint16,
            IndexFormat.UInt32 => IndexType.Uint32,
            _ => IndexType.Uint16
        };
    }

    public static GeometryTypeKHR Vulkan(RayTracingGeometryType rayTracingGeometryType)
    {
        return rayTracingGeometryType switch
        {
            RayTracingGeometryType.Triangles => GeometryTypeKHR.TrianglesKhr,
            RayTracingGeometryType.AABBs => GeometryTypeKHR.AabbsKhr,
            _ => GeometryTypeKHR.TrianglesKhr
        };
    }

    public static GeometryFlagsKHR Vulkan(RayTracingGeometryFlags rayTracingGeometryFlags)
    {
        GeometryFlagsKHR result = GeometryFlagsKHR.None;

        if (rayTracingGeometryFlags.HasFlag(RayTracingGeometryFlags.Opaque))
        {
            result |= GeometryFlagsKHR.OpaqueBitKhr;
        }

        if (rayTracingGeometryFlags.HasFlag(RayTracingGeometryFlags.NoDuplicateAnyHitInvocation))
        {
            result |= GeometryFlagsKHR.NoDuplicateAnyHitInvocationBitKhr;
        }

        return result;
    }

    public static BuildAccelerationStructureFlagsKHR Vulkan(AccelerationStructureBuildFlags accelerationStructureBuildFlags)
    {
        BuildAccelerationStructureFlagsKHR result = BuildAccelerationStructureFlagsKHR.None;

        if (accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.AllowUpdate) || accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.PerformUpdate))
        {
            result |= BuildAccelerationStructureFlagsKHR.AllowUpdateBitKhr;
        }

        if (accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.AllowCompactation))
        {
            result |= BuildAccelerationStructureFlagsKHR.AllowCompactionBitKhr;
        }

        if (accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.PreferFastTrace))
        {
            result |= BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr;
        }

        if (accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.PreferFastBuild))
        {
            result |= BuildAccelerationStructureFlagsKHR.PreferFastBuildBitKhr;
        }

        if (accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.MinimizeMemory))
        {
            result |= BuildAccelerationStructureFlagsKHR.LowMemoryBitKhr;
        }

        return result;
    }

    public static GeometryInstanceFlagsKHR Vulkan(RayTracingInstanceFlags rayTracingInstanceFlags)
    {
        GeometryInstanceFlagsKHR result = GeometryInstanceFlagsKHR.None;

        if (rayTracingInstanceFlags.HasFlag(RayTracingInstanceFlags.TriangleCullDisable))
        {
            result |= GeometryInstanceFlagsKHR.TriangleFacingCullDisableBitKhr;
        }

        if (rayTracingInstanceFlags.HasFlag(RayTracingInstanceFlags.TriangleFrontCounterClockwise))
        {
            result |= GeometryInstanceFlagsKHR.TriangleFrontCounterclockwiseBitKhr;
        }

        if (rayTracingInstanceFlags.HasFlag(RayTracingInstanceFlags.ForceOpaque))
        {
            result |= GeometryInstanceFlagsKHR.ForceOpaqueBitKhr;
        }

        if (rayTracingInstanceFlags.HasFlag(RayTracingInstanceFlags.ForceNoOpaque))
        {
            result |= GeometryInstanceFlagsKHR.ForceNoOpaqueBitKhr;
        }

        return result;
    }
}
