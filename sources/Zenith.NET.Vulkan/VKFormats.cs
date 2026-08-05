using System.Numerics;
using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal static class VKFormats
{
    public static BuildAccelerationStructureFlagsKHR Vulkan(AccelerationStructureBuildFlags accelerationStructureBuildFlags)
    {
        BuildAccelerationStructureFlagsKHR result = default;

        if (accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.AllowUpdate))
        {
            result |= BuildAccelerationStructureFlagsKHR.AllowUpdateBitKhr;
        }

        if (accelerationStructureBuildFlags.HasFlag(AccelerationStructureBuildFlags.AllowCompaction))
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

    public static SamplerAddressMode Vulkan(AddressMode addressMode)
    {
        return addressMode switch
        {
            AddressMode.Wrap => SamplerAddressMode.Repeat,
            AddressMode.Mirror => SamplerAddressMode.MirroredRepeat,
            AddressMode.Clamp => SamplerAddressMode.ClampToEdge,
            AddressMode.Border => SamplerAddressMode.ClampToBorder,
            _ => default
        };
    }

    public static (PipelineStageFlags2 Stage, AccessFlags2 Access) Vulkan(BarrierStages barrierStages)
    {
        if (barrierStages is BarrierStages.None)
        {
            return (PipelineStageFlags2.None, AccessFlags2.None);
        }

        PipelineStageFlags2 stage = default;
        AccessFlags2 access = default;

        if (barrierStages.HasFlag(BarrierStages.VertexShading))
        {
            stage |= PipelineStageFlags2.DrawIndirectBit | PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.IndexInputBit | PipelineStageFlags2.VertexAttributeInputBit;
            access |= AccessFlags2.IndirectCommandReadBit | AccessFlags2.IndexReadBit | AccessFlags2.VertexAttributeReadBit | AccessFlags2.UniformReadBit | AccessFlags2.ShaderReadBit | AccessFlags2.ShaderWriteBit | AccessFlags2.AccelerationStructureReadBitKhr;
        }

        if (barrierStages.HasFlag(BarrierStages.FragmentShading))
        {
            stage |= PipelineStageFlags2.FragmentShaderBit | PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit | PipelineStageFlags2.ColorAttachmentOutputBit;
            access |= AccessFlags2.UniformReadBit | AccessFlags2.ShaderReadBit | AccessFlags2.ShaderWriteBit | AccessFlags2.ColorAttachmentReadBit | AccessFlags2.ColorAttachmentWriteBit | AccessFlags2.DepthStencilAttachmentReadBit | AccessFlags2.DepthStencilAttachmentWriteBit | AccessFlags2.AccelerationStructureReadBitKhr;
        }

        if (barrierStages.HasFlag(BarrierStages.ComputeShading))
        {
            stage |= PipelineStageFlags2.DrawIndirectBit | PipelineStageFlags2.ComputeShaderBit;
            access |= AccessFlags2.IndirectCommandReadBit | AccessFlags2.UniformReadBit | AccessFlags2.ShaderReadBit | AccessFlags2.ShaderWriteBit | AccessFlags2.AccelerationStructureReadBitKhr;
        }

        if (barrierStages.HasFlag(BarrierStages.Copy))
        {
            stage |= PipelineStageFlags2.CopyBit;
            access |= AccessFlags2.TransferReadBit | AccessFlags2.TransferWriteBit;
        }

        if (barrierStages.HasFlag(BarrierStages.Resolve))
        {
            stage |= PipelineStageFlags2.ResolveBit;
            access |= AccessFlags2.TransferReadBit | AccessFlags2.TransferWriteBit;
        }

        if (barrierStages.HasFlag(BarrierStages.All))
        {
            stage = PipelineStageFlags2.AllCommandsBit;
            access = AccessFlags2.MemoryReadBit | AccessFlags2.MemoryWriteBit;
        }

        return (stage, access);
    }

    public static VkBlendFactor Vulkan(BlendFactor blendFactor)
    {
        return blendFactor switch
        {
            BlendFactor.Zero => VkBlendFactor.Zero,
            BlendFactor.One => VkBlendFactor.One,
            BlendFactor.SrcColor => VkBlendFactor.SrcColor,
            BlendFactor.OneMinusSrcColor => VkBlendFactor.OneMinusSrcColor,
            BlendFactor.DstColor => VkBlendFactor.DstColor,
            BlendFactor.OneMinusDstColor => VkBlendFactor.OneMinusDstColor,
            BlendFactor.SrcAlpha => VkBlendFactor.SrcAlpha,
            BlendFactor.OneMinusSrcAlpha => VkBlendFactor.OneMinusSrcAlpha,
            BlendFactor.DstAlpha => VkBlendFactor.DstAlpha,
            BlendFactor.OneMinusDstAlpha => VkBlendFactor.OneMinusDstAlpha,
            BlendFactor.Constant => VkBlendFactor.ConstantColor,
            BlendFactor.OneMinusConstant => VkBlendFactor.OneMinusConstantColor,
            _ => default
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
            _ => default
        };
    }

    public static VkBorderColor Vulkan(BorderColor borderColor)
    {
        return borderColor switch
        {
            BorderColor.TransparentBlack => VkBorderColor.FloatTransparentBlack,
            BorderColor.OpaqueBlack => VkBorderColor.FloatOpaqueBlack,
            BorderColor.OpaqueWhite => VkBorderColor.FloatOpaqueWhite,
            _ => default
        };
    }

    public static BufferUsageFlags Vulkan(BufferUsages bufferUsages, bool rayTracingSupported)
    {
        BufferUsageFlags result = BufferUsageFlags.ShaderDeviceAddressBit;

        if (bufferUsages.HasFlag(BufferUsages.Vertex))
        {
            result |= BufferUsageFlags.VertexBufferBit;

            if (rayTracingSupported)
            {
                result |= BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
            }
        }

        if (bufferUsages.HasFlag(BufferUsages.Index))
        {
            result |= BufferUsageFlags.IndexBufferBit;

            if (rayTracingSupported)
            {
                result |= BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
            }
        }

        if (bufferUsages.HasFlag(BufferUsages.Indirect))
        {
            result |= BufferUsageFlags.IndirectBufferBit;
        }

        if (bufferUsages.HasFlag(BufferUsages.Constant))
        {
            result |= BufferUsageFlags.UniformBufferBit;
        }

        if (bufferUsages.HasFlag(BufferUsages.StorageReadOnly) || bufferUsages.HasFlag(BufferUsages.StorageReadWrite))
        {
            result |= BufferUsageFlags.StorageBufferBit;

            if (rayTracingSupported)
            {
                result |= BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
            }
        }

        if (bufferUsages.HasFlag(BufferUsages.TransferSrc))
        {
            result |= BufferUsageFlags.TransferSrcBit;
        }

        if (bufferUsages.HasFlag(BufferUsages.TransferDst))
        {
            result |= BufferUsageFlags.TransferDstBit;
        }

        return result;
    }

    public static ColorComponentFlags Vulkan(ColorWrites colorWrites)
    {
        ColorComponentFlags result = default;

        if (colorWrites.HasFlag(ColorWrites.Red))
        {
            result |= ColorComponentFlags.RBit;
        }

        if (colorWrites.HasFlag(ColorWrites.Green))
        {
            result |= ColorComponentFlags.GBit;
        }

        if (colorWrites.HasFlag(ColorWrites.Blue))
        {
            result |= ColorComponentFlags.BBit;
        }

        if (colorWrites.HasFlag(ColorWrites.Alpha))
        {
            result |= ColorComponentFlags.ABit;
        }

        return result;
    }

    public static VkCompareOp Vulkan(CompareOp compareOp)
    {
        return compareOp switch
        {
            CompareOp.Never => VkCompareOp.Never,
            CompareOp.Less => VkCompareOp.Less,
            CompareOp.Equal => VkCompareOp.Equal,
            CompareOp.LessEqual => VkCompareOp.LessOrEqual,
            CompareOp.Greater => VkCompareOp.Greater,
            CompareOp.NotEqual => VkCompareOp.NotEqual,
            CompareOp.GreaterEqual => VkCompareOp.GreaterOrEqual,
            CompareOp.Always => VkCompareOp.Always,
            _ => default
        };
    }

    public static CullModeFlags Vulkan(CullMode cullMode)
    {
        return cullMode switch
        {
            CullMode.None => CullModeFlags.None,
            CullMode.Front => CullModeFlags.FrontBit,
            CullMode.Back => CullModeFlags.BackBit,
            _ => default
        };
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

            ElementFormat.UByte1UNorm => Format.R8Unorm,
            ElementFormat.UByte2UNorm => Format.R8G8Unorm,
            ElementFormat.UByte4UNorm => Format.R8G8B8A8Unorm,

            ElementFormat.Byte1SNorm => Format.R8SNorm,
            ElementFormat.Byte2SNorm => Format.R8G8SNorm,
            ElementFormat.Byte4SNorm => Format.R8G8B8A8SNorm,

            ElementFormat.UShort1 => Format.R16Uint,
            ElementFormat.UShort2 => Format.R16G16Uint,
            ElementFormat.UShort4 => Format.R16G16B16A16Uint,

            ElementFormat.Short1 => Format.R16Sint,
            ElementFormat.Short2 => Format.R16G16Sint,
            ElementFormat.Short4 => Format.R16G16B16A16Sint,

            ElementFormat.UShort1UNorm => Format.R16Unorm,
            ElementFormat.UShort2UNorm => Format.R16G16Unorm,
            ElementFormat.UShort4UNorm => Format.R16G16B16A16Unorm,

            ElementFormat.Short1SNorm => Format.R16SNorm,
            ElementFormat.Short2SNorm => Format.R16G16SNorm,
            ElementFormat.Short4SNorm => Format.R16G16B16A16SNorm,

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

            _ => default
        };
    }

    public static PolygonMode Vulkan(FillMode fillMode)
    {
        return fillMode switch
        {
            FillMode.Solid => PolygonMode.Fill,
            FillMode.Wireframe => PolygonMode.Line,
            _ => default
        };
    }

    public static (Filter Filter, SamplerMipmapMode MipmapMode) Vulkan(FilterMode filterMode)
    {
        return
        (
            filterMode switch
            {
                FilterMode.Point => Filter.Nearest,
                FilterMode.Linear => Filter.Linear,
                _ => default
            },
            filterMode switch
            {
                FilterMode.Point => SamplerMipmapMode.Nearest,
                FilterMode.Linear => SamplerMipmapMode.Linear,
                _ => default
            }
        );
    }

    public static VkFrontFace Vulkan(FrontFace frontFace)
    {
        return frontFace switch
        {
            FrontFace.CounterClockwise => VkFrontFace.CounterClockwise,
            FrontFace.Clockwise => VkFrontFace.Clockwise,
            _ => default
        };
    }

    public static IndexType Vulkan(IndexFormat indexFormat)
    {
        return indexFormat switch
        {
            IndexFormat.UInt16 => IndexType.Uint16,
            IndexFormat.UInt32 => IndexType.Uint32,
            _ => default
        };
    }

    public static AttachmentLoadOp Vulkan(LoadOp loadOp)
    {
        return loadOp switch
        {
            LoadOp.Load => AttachmentLoadOp.Load,
            LoadOp.Clear => AttachmentLoadOp.Clear,
            LoadOp.DontCare => AttachmentLoadOp.DontCare,
            _ => default
        };
    }

    public static unsafe TransformMatrixKHR Vulkan(Matrix4x4 matrix4x4)
    {
        TransformMatrixKHR result = new();
        result.Matrix[0] = matrix4x4.M11;
        result.Matrix[1] = matrix4x4.M12;
        result.Matrix[2] = matrix4x4.M13;
        result.Matrix[3] = matrix4x4.M14;
        result.Matrix[4] = matrix4x4.M21;
        result.Matrix[5] = matrix4x4.M22;
        result.Matrix[6] = matrix4x4.M23;
        result.Matrix[7] = matrix4x4.M24;
        result.Matrix[8] = matrix4x4.M31;
        result.Matrix[9] = matrix4x4.M32;
        result.Matrix[10] = matrix4x4.M33;
        result.Matrix[11] = matrix4x4.M34;

        return result;
    }

    public static ExternalMemoryHandleTypeFlags Vulkan(NativeTextureType nativeTextureType)
    {
        return nativeTextureType switch
        {
            NativeTextureType.D3D11TextureNtHandle => ExternalMemoryHandleTypeFlags.D3D11TextureBit,
            NativeTextureType.D3D12ResourceNtHandle => ExternalMemoryHandleTypeFlags.D3D12ResourceBit,
            NativeTextureType.VulkanOpaqueNtHandle => ExternalMemoryHandleTypeFlags.OpaqueWin32Bit,
            NativeTextureType.VulkanOpaquePosixFileDescriptor => ExternalMemoryHandleTypeFlags.OpaqueFDBit,
            NativeTextureType.VulkanAndroidHardwareBuffer => ExternalMemoryHandleTypeFlags.AndroidHardwareBufferBitAndroid,
            _ => default
        };
    }

    public static (Format Format, ImageAspectFlags AspectFlags) Vulkan(PixelFormat pixelFormat)
    {
        Format format = pixelFormat switch
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
            PixelFormat.R8G8B8A8SNorm => Format.R8G8B8A8SNorm,
            PixelFormat.R8G8B8A8UInt => Format.R8G8B8A8Uint,
            PixelFormat.R8G8B8A8SInt => Format.R8G8B8A8Sint,
            PixelFormat.R8G8B8A8SRgb => Format.R8G8B8A8Srgb,

            PixelFormat.R16G16B16A16UNorm => Format.R16G16B16A16Unorm,
            PixelFormat.R16G16B16A16SNorm => Format.R16G16B16A16SNorm,
            PixelFormat.R16G16B16A16UInt => Format.R16G16B16A16Uint,
            PixelFormat.R16G16B16A16SInt => Format.R16G16B16A16Sint,
            PixelFormat.R16G16B16A16Float => Format.R16G16B16A16Sfloat,

            PixelFormat.R32G32B32A32UInt => Format.R32G32B32A32Uint,
            PixelFormat.R32G32B32A32SInt => Format.R32G32B32A32Sint,
            PixelFormat.R32G32B32A32Float => Format.R32G32B32A32Sfloat,

            PixelFormat.B8G8R8A8UNorm => Format.B8G8R8A8Unorm,
            PixelFormat.B8G8R8A8SRgb => Format.B8G8R8A8Srgb,

            PixelFormat.D16UNorm => Format.D16Unorm,
            PixelFormat.D24UNormS8UInt => Format.D24UnormS8Uint,
            PixelFormat.D32Float => Format.D32Sfloat,
            PixelFormat.D32FloatS8UInt => Format.D32SfloatS8Uint,

            PixelFormat.BC4UNorm => Format.BC4UnormBlock,
            PixelFormat.BC4SNorm => Format.BC4SNormBlock,
            PixelFormat.BC5UNorm => Format.BC5UnormBlock,
            PixelFormat.BC5SNorm => Format.BC5SNormBlock,
            PixelFormat.BC6HUFloat => Format.BC6HUfloatBlock,
            PixelFormat.BC6HSFloat => Format.BC6HSfloatBlock,
            PixelFormat.BC7UNorm => Format.BC7UnormBlock,
            PixelFormat.BC7SRgb => Format.BC7SrgbBlock,

            PixelFormat.ETC2UNorm => Format.Etc2R8G8B8UnormBlock,
            PixelFormat.ETC2SRgb => Format.Etc2R8G8B8SrgbBlock,
            PixelFormat.ETC2A1UNorm => Format.Etc2R8G8B8A1UnormBlock,
            PixelFormat.ETC2A1SRgb => Format.Etc2R8G8B8A1SrgbBlock,
            PixelFormat.ETC2A8UNorm => Format.Etc2R8G8B8A8UnormBlock,
            PixelFormat.ETC2A8SRgb => Format.Etc2R8G8B8A8SrgbBlock,

            PixelFormat.ASTC4x4UNorm => Format.Astc4x4UnormBlock,
            PixelFormat.ASTC4x4SRgb => Format.Astc4x4SrgbBlock,
            PixelFormat.ASTC4x4Float => Format.Astc4x4SfloatBlock,
            PixelFormat.ASTC5x5UNorm => Format.Astc5x5UnormBlock,
            PixelFormat.ASTC5x5SRgb => Format.Astc5x5SrgbBlock,
            PixelFormat.ASTC5x5Float => Format.Astc5x5SfloatBlock,
            PixelFormat.ASTC6x6UNorm => Format.Astc6x6UnormBlock,
            PixelFormat.ASTC6x6SRgb => Format.Astc6x6SrgbBlock,
            PixelFormat.ASTC6x6Float => Format.Astc6x6SfloatBlock,
            PixelFormat.ASTC8x8UNorm => Format.Astc8x8UnormBlock,
            PixelFormat.ASTC8x8SRgb => Format.Astc8x8SrgbBlock,
            PixelFormat.ASTC8x8Float => Format.Astc8x8SfloatBlock,
            PixelFormat.ASTC10x10UNorm => Format.Astc10x10UnormBlock,
            PixelFormat.ASTC10x10SRgb => Format.Astc10x10SrgbBlock,
            PixelFormat.ASTC10x10Float => Format.Astc10x10SfloatBlock,
            PixelFormat.ASTC12x12UNorm => Format.Astc12x12UnormBlock,
            PixelFormat.ASTC12x12SRgb => Format.Astc12x12SrgbBlock,
            PixelFormat.ASTC12x12Float => Format.Astc12x12SfloatBlock,

            _ => default
        };

        ImageAspectFlags aspectFlags = default;

        if (ZenithHelper.HasDepth(pixelFormat))
        {
            aspectFlags |= ImageAspectFlags.DepthBit;
        }

        if (ZenithHelper.HasStencil(pixelFormat))
        {
            aspectFlags |= ImageAspectFlags.StencilBit;
        }

        if (aspectFlags is 0)
        {
            aspectFlags = ImageAspectFlags.ColorBit;
        }

        return (format, aspectFlags);
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
            _ => default
        };
    }

    public static VkQueryType Vulkan(QueryType queryType)
    {
        return queryType switch
        {
            QueryType.Occlusion or
            QueryType.BinaryOcclusion => VkQueryType.Occlusion,

            QueryType.Timestamp => VkQueryType.Timestamp,

            _ => default
        };
    }

    public static GeometryTypeKHR Vulkan(RayTracingGeometryType rayTracingGeometryType)
    {
        return rayTracingGeometryType switch
        {
            RayTracingGeometryType.Triangle => GeometryTypeKHR.TrianglesKhr,
            RayTracingGeometryType.Aabb => GeometryTypeKHR.AabbsKhr,
            _ => default
        };
    }

    public static GeometryInstanceFlagsKHR Vulkan(RayTracingInstanceFlags rayTracingInstanceFlags)
    {
        GeometryInstanceFlagsKHR result = default;

        if (rayTracingInstanceFlags.HasFlag(RayTracingInstanceFlags.FrontCounterClockwise))
        {
            result |= GeometryInstanceFlagsKHR.TriangleFrontCounterclockwiseBitKhr;
        }

        if (rayTracingInstanceFlags.HasFlag(RayTracingInstanceFlags.DisableCull))
        {
            result |= GeometryInstanceFlagsKHR.TriangleFacingCullDisableBitKhr;
        }

        if (rayTracingInstanceFlags.HasFlag(RayTracingInstanceFlags.ForceOpaque))
        {
            result |= GeometryInstanceFlagsKHR.ForceOpaqueBitKhr;
        }

        if (rayTracingInstanceFlags.HasFlag(RayTracingInstanceFlags.ForceNonOpaque))
        {
            result |= GeometryInstanceFlagsKHR.ForceNoOpaqueBitKhr;
        }

        return result;
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
            _ => default
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
            _ => default
        };
    }

    public static AttachmentStoreOp Vulkan(StoreOp storeOp)
    {
        return storeOp switch
        {
            StoreOp.Store => AttachmentStoreOp.Store,
            StoreOp.DontCare => AttachmentStoreOp.DontCare,
            _ => default
        };
    }

    public static (PipelineStageFlags2 Stage, AccessFlags2 Access, ImageLayout Layout) Vulkan(TextureLayout textureLayout)
    {
        return textureLayout switch
        {
            TextureLayout.Undefined => (PipelineStageFlags2.None, AccessFlags2.None, ImageLayout.Undefined),
            TextureLayout.Common => (PipelineStageFlags2.AllCommandsBit, AccessFlags2.MemoryReadBit | AccessFlags2.MemoryWriteBit, ImageLayout.General),
            TextureLayout.Sampled => (PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.FragmentShaderBit | PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderReadBit, ImageLayout.ShaderReadOnlyOptimal),
            TextureLayout.Storage => (PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.FragmentShaderBit | PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderReadBit | AccessFlags2.ShaderWriteBit, ImageLayout.General),
            TextureLayout.ColorAttachment => (PipelineStageFlags2.ColorAttachmentOutputBit, AccessFlags2.ColorAttachmentReadBit | AccessFlags2.ColorAttachmentWriteBit, ImageLayout.ColorAttachmentOptimal),
            TextureLayout.DepthStencilAttachment => (PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit, AccessFlags2.DepthStencilAttachmentReadBit | AccessFlags2.DepthStencilAttachmentWriteBit, ImageLayout.DepthStencilAttachmentOptimal),
            TextureLayout.DepthStencilReadOnly => (PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit, AccessFlags2.DepthStencilAttachmentReadBit, ImageLayout.DepthStencilReadOnlyOptimal),
            TextureLayout.CopySrc => (PipelineStageFlags2.CopyBit, AccessFlags2.TransferReadBit, ImageLayout.TransferSrcOptimal),
            TextureLayout.CopyDst => (PipelineStageFlags2.CopyBit, AccessFlags2.TransferWriteBit, ImageLayout.TransferDstOptimal),
            TextureLayout.ResolveSrc => (PipelineStageFlags2.ResolveBit, AccessFlags2.TransferReadBit, ImageLayout.TransferSrcOptimal),
            TextureLayout.ResolveDst => (PipelineStageFlags2.ResolveBit, AccessFlags2.TransferWriteBit, ImageLayout.TransferDstOptimal),
            TextureLayout.Present => (PipelineStageFlags2.AllCommandsBit, AccessFlags2.None, ImageLayout.PresentSrcKhr),
            _ => (default, default, default)
        };
    }

    public static (ImageType Type, ImageViewType ViewType) Vulkan(TextureType textureType)
    {
        return
        (
            textureType switch
            {
                TextureType.Texture1D or
                TextureType.Texture1DArray => ImageType.Type1D,

                TextureType.Texture2D or
                TextureType.Texture2DArray or
                TextureType.TextureCube or
                TextureType.TextureCubeArray => ImageType.Type2D,

                TextureType.Texture3D => ImageType.Type3D,

                _ => default
            },
            textureType switch
            {
                TextureType.Texture1D => ImageViewType.Type1D,
                TextureType.Texture1DArray => ImageViewType.Type1DArray,
                TextureType.Texture2D => ImageViewType.Type2D,
                TextureType.Texture2DArray => ImageViewType.Type2DArray,
                TextureType.Texture3D => ImageViewType.Type3D,
                TextureType.TextureCube => ImageViewType.TypeCube,
                TextureType.TextureCubeArray => ImageViewType.TypeCubeArray,
                _ => default
            }
        );
    }

    public static ImageUsageFlags Vulkan(TextureUsages textureUsages)
    {
        ImageUsageFlags result = default;

        if (textureUsages.HasFlag(TextureUsages.Sampled))
        {
            result |= ImageUsageFlags.SampledBit;
        }

        if (textureUsages.HasFlag(TextureUsages.Storage))
        {
            result |= ImageUsageFlags.StorageBit;
        }

        if (textureUsages.HasFlag(TextureUsages.ColorAttachment))
        {
            result |= ImageUsageFlags.ColorAttachmentBit;
        }

        if (textureUsages.HasFlag(TextureUsages.DepthStencilAttachment))
        {
            result |= ImageUsageFlags.DepthStencilAttachmentBit;
        }

        if (textureUsages.HasFlag(TextureUsages.TransferSrc))
        {
            result |= ImageUsageFlags.TransferSrcBit;
        }

        if (textureUsages.HasFlag(TextureUsages.TransferDst))
        {
            result |= ImageUsageFlags.TransferDstBit;
        }

        return result;
    }
}
