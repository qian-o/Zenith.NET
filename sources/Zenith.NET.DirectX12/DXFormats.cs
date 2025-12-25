using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal static class DXFormats
{
    public static (ResourceFlags Flags, ResourceStates States, HeapType Type) DirectX12(BufferUsageFlags bufferUsageFlags)
    {
        ResourceFlags flags = ResourceFlags.None;

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.AccelerationStructure) || bufferUsageFlags.HasFlag(BufferUsageFlags.UnorderedAccess))
        {
            flags |= ResourceFlags.AllowUnorderedAccess;

            if (bufferUsageFlags.HasFlag(BufferUsageFlags.AccelerationStructure))
            {
                flags |= ResourceFlags.RaytracingAccelerationStructure;
            }
        }

        ResourceStates states = ResourceStates.Common;

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.Vertex) || bufferUsageFlags.HasFlag(BufferUsageFlags.Constant))
        {
            states |= ResourceStates.VertexAndConstantBuffer;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.Index))
        {
            states |= ResourceStates.IndexBuffer;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.Indirect))
        {
            states |= ResourceStates.IndirectArgument;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.AccelerationStructure))
        {
            states |= ResourceStates.RaytracingAccelerationStructure;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.ShaderResource))
        {
            states |= ResourceStates.AllShaderResource;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.UnorderedAccess))
        {
            states |= ResourceStates.UnorderedAccess;
        }

        HeapType type = HeapType.Default;

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.MapRead))
        {
            states = ResourceStates.CopyDest;

            type = HeapType.Readback;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.MapWrite))
        {
            states = ResourceStates.GenericRead;

            type = HeapType.Upload;
        }

        return (flags, states, type);
    }

    public static ResourceDimension DirectX12(TextureType textureType)
    {
        return textureType switch
        {
            TextureType.Texture1D or
            TextureType.Texture1DArray => ResourceDimension.Texture1D,

            TextureType.Texture2D or
            TextureType.Texture2DArray or
            TextureType.TextureCube or
            TextureType.TextureCubeArray => ResourceDimension.Texture2D,

            TextureType.Texture3D => ResourceDimension.Texture3D,

            _ => ResourceDimension.Texture1D
        };
    }

    public static Format DirectX12(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.R8UNorm => Format.FormatR8Unorm,
            PixelFormat.R8SNorm => Format.FormatR8SNorm,
            PixelFormat.R8UInt => Format.FormatR8Uint,
            PixelFormat.R8SInt => Format.FormatR8Sint,

            PixelFormat.R16UNorm => Format.FormatR16Unorm,
            PixelFormat.R16SNorm => Format.FormatR16SNorm,
            PixelFormat.R16UInt => Format.FormatR16Uint,
            PixelFormat.R16SInt => Format.FormatR16Sint,
            PixelFormat.R16Float => Format.FormatR16Float,

            PixelFormat.R32UInt => Format.FormatR32Uint,
            PixelFormat.R32SInt => Format.FormatR32Sint,
            PixelFormat.R32Float => Format.FormatR32Float,

            PixelFormat.R8G8UNorm => Format.FormatR8G8Unorm,
            PixelFormat.R8G8SNorm => Format.FormatR8G8SNorm,
            PixelFormat.R8G8UInt => Format.FormatR8G8Uint,
            PixelFormat.R8G8SInt => Format.FormatR8G8Sint,

            PixelFormat.R16G16UNorm => Format.FormatR16G16Unorm,
            PixelFormat.R16G16SNorm => Format.FormatR16G16SNorm,
            PixelFormat.R16G16UInt => Format.FormatR16G16Uint,
            PixelFormat.R16G16SInt => Format.FormatR16G16Sint,
            PixelFormat.R16G16Float => Format.FormatR16G16Float,

            PixelFormat.R32G32UInt => Format.FormatR32G32Uint,
            PixelFormat.R32G32SInt => Format.FormatR32G32Sint,
            PixelFormat.R32G32Float => Format.FormatR32G32Float,

            PixelFormat.R32G32B32UInt => Format.FormatR32G32B32Uint,
            PixelFormat.R32G32B32SInt => Format.FormatR32G32B32Sint,
            PixelFormat.R32G32B32Float => Format.FormatR32G32B32Float,

            PixelFormat.R8G8B8A8UNorm => Format.FormatR8G8B8A8Unorm,
            PixelFormat.R8G8B8A8SNorm => Format.FormatR8G8B8A8SNorm,
            PixelFormat.R8G8B8A8UInt => Format.FormatR8G8B8A8Uint,
            PixelFormat.R8G8B8A8SInt => Format.FormatR8G8B8A8Sint,
            PixelFormat.R8G8B8A8SRgb => Format.FormatR8G8B8A8UnormSrgb,

            PixelFormat.R16G16B16A16UNorm => Format.FormatR16G16B16A16Unorm,
            PixelFormat.R16G16B16A16SNorm => Format.FormatR16G16B16A16SNorm,
            PixelFormat.R16G16B16A16UInt => Format.FormatR16G16B16A16Uint,
            PixelFormat.R16G16B16A16SInt => Format.FormatR16G16B16A16Sint,
            PixelFormat.R16G16B16A16Float => Format.FormatR16G16B16A16Float,

            PixelFormat.R32G32B32A32UInt => Format.FormatR32G32B32A32Uint,
            PixelFormat.R32G32B32A32SInt => Format.FormatR32G32B32A32Sint,
            PixelFormat.R32G32B32A32Float => Format.FormatR32G32B32A32Float,

            PixelFormat.B8G8R8A8UNorm => Format.FormatB8G8R8A8Unorm,
            PixelFormat.B8G8R8A8SRgb => Format.FormatB8G8R8A8UnormSrgb,

            PixelFormat.D24UNormS8UInt => Format.FormatD24UnormS8Uint,
            PixelFormat.D32FloatS8UInt => Format.FormatD32FloatS8X24Uint,

            PixelFormat.BC4UNorm => Format.FormatBC4Unorm,
            PixelFormat.BC4SNorm => Format.FormatBC4SNorm,

            PixelFormat.BC5UNorm => Format.FormatBC5Unorm,
            PixelFormat.BC5SNorm => Format.FormatBC5SNorm,

            PixelFormat.BC6HUFloat => Format.FormatBC6HUF16,
            PixelFormat.BC6HSFloat => Format.FormatBC6HSF16,

            PixelFormat.BC7UNorm => Format.FormatBC7Unorm,
            PixelFormat.BC7SRgb => Format.FormatBC7UnormSrgb,

            _ => Format.FormatUnknown
        };
    }

    public static SampleDesc DirectX12(SampleCount sampleCount)
    {
        return sampleCount switch
        {
            SampleCount.Count1 => new() { Count = 1 },
            SampleCount.Count2 => new() { Count = 2 },
            SampleCount.Count4 => new() { Count = 4 },
            SampleCount.Count8 => new() { Count = 8 },
            SampleCount.Count16 => new() { Count = 16 },
            SampleCount.Count32 => new() { Count = 32 },
            _ => new() { Count = 1 }
        };
    }

    public static (ResourceFlags Flags, ResourceStates States) DirectX12(TextureUsageFlags textureUsageFlags)
    {
        ResourceFlags flags = ResourceFlags.None;

        if (textureUsageFlags.HasFlag(TextureUsageFlags.RenderTarget))
        {
            flags |= ResourceFlags.AllowRenderTarget;
        }

        if (textureUsageFlags.HasFlag(TextureUsageFlags.DepthStencil))
        {
            flags |= ResourceFlags.AllowDepthStencil;
        }

        ResourceStates states = ResourceStates.Common;

        if (textureUsageFlags.HasFlag(TextureUsageFlags.RenderTarget))
        {
            states |= ResourceStates.RenderTarget;
        }

        if (textureUsageFlags.HasFlag(TextureUsageFlags.DepthStencil))
        {
            states |= ResourceStates.DepthWrite;
        }

        if (textureUsageFlags.HasFlag(TextureUsageFlags.ShaderResource))
        {
            states |= ResourceStates.AllShaderResource;
        }

        if (textureUsageFlags.HasFlag(TextureUsageFlags.UnorderedAccess))
        {
            states |= ResourceStates.UnorderedAccess;
        }

        return (flags, states);
    }

    public static (DxFilter Filter, DxComparisonFunc ComparisonFunc) DirectX12(Filter filter, ComparisonFunc comparisonFunc)
    {
        bool isComparison = comparisonFunc is not ComparisonFunc.Never and not ComparisonFunc.Always;

        return
        (
            filter switch
            {
                Filter.MinPointMagPointMipPoint => isComparison ? DxFilter.ComparisonMinMagMipPoint : DxFilter.MinMagMipPoint,
                Filter.MinPointMagPointMipLinear => isComparison ? DxFilter.ComparisonMinMagPointMipLinear : DxFilter.MinMagPointMipLinear,
                Filter.MinPointMagLinearMipPoint => isComparison ? DxFilter.ComparisonMinPointMagLinearMipPoint : DxFilter.MinPointMagLinearMipPoint,
                Filter.MinPointMagLinearMipLinear => isComparison ? DxFilter.ComparisonMinPointMagMipLinear : DxFilter.MinPointMagMipLinear,
                Filter.MinLinearMagPointMipPoint => isComparison ? DxFilter.ComparisonMinLinearMagMipPoint : DxFilter.MinLinearMagMipPoint,
                Filter.MinLinearMagPointMipLinear => isComparison ? DxFilter.ComparisonMinLinearMagPointMipLinear : DxFilter.MinLinearMagPointMipLinear,
                Filter.MinLinearMagLinearMipPoint => isComparison ? DxFilter.ComparisonMinMagLinearMipPoint : DxFilter.MinMagLinearMipPoint,
                Filter.MinLinearMagLinearMipLinear => isComparison ? DxFilter.ComparisonMinMagMipLinear : DxFilter.MinMagMipLinear,
                Filter.Anisotropic => isComparison ? DxFilter.ComparisonAnisotropic : DxFilter.Anisotropic,
                _ => DxFilter.MinMagMipPoint
            },
            comparisonFunc switch
            {
                ComparisonFunc.Never => DxComparisonFunc.Never,
                ComparisonFunc.Less => DxComparisonFunc.Less,
                ComparisonFunc.Equal => DxComparisonFunc.Equal,
                ComparisonFunc.LessEqual => DxComparisonFunc.LessEqual,
                ComparisonFunc.Greater => DxComparisonFunc.Greater,
                ComparisonFunc.NotEqual => DxComparisonFunc.NotEqual,
                ComparisonFunc.GreaterEqual => DxComparisonFunc.GreaterEqual,
                ComparisonFunc.Always => DxComparisonFunc.Always,
                _ => DxComparisonFunc.None
            }
        );
    }

    public static TextureAddressMode DirectX12(AddressMode addressMode)
    {
        return addressMode switch
        {
            AddressMode.Wrap => TextureAddressMode.Wrap,
            AddressMode.Mirror => TextureAddressMode.Mirror,
            AddressMode.Clamp => TextureAddressMode.Clamp,
            AddressMode.Border => TextureAddressMode.Border,
            _ => TextureAddressMode.Wrap
        };
    }

    public static DescriptorRangeType DirectX12(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.ConstantBuffer => DescriptorRangeType.Cbv,

            ResourceType.StructuredBuffer or
            ResourceType.Texture or
            ResourceType.AccelerationStructure => DescriptorRangeType.Srv,

            ResourceType.StructuredBufferReadWrite or
            ResourceType.TextureReadWrite => DescriptorRangeType.Uav,

            ResourceType.Sampler => DescriptorRangeType.Sampler,

            _ => DescriptorRangeType.Srv
        };
    }

    public static ShaderVisibility DirectX12(ShaderStageFlags shaderStageFlags)
    {
        if (shaderStageFlags.HasFlag(ShaderStageFlags.Vertex))
        {
            return ShaderVisibility.Vertex;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Hull))
        {
            return ShaderVisibility.Hull;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Domain))
        {
            return ShaderVisibility.Domain;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Geometry))
        {
            return ShaderVisibility.Geometry;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Pixel))
        {
            return ShaderVisibility.Pixel;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Amplification))
        {
            return ShaderVisibility.Amplification;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Mesh))
        {
            return ShaderVisibility.Mesh;
        }

        return ShaderVisibility.All;
    }

    public static DxFillMode DirectX12(FillMode fillMode)
    {
        return fillMode switch
        {
            FillMode.Solid => DxFillMode.Solid,
            FillMode.Wireframe => DxFillMode.Wireframe,
            _ => DxFillMode.None
        };
    }

    public static DxCullMode DirectX12(CullMode cullMode)
    {
        return cullMode switch
        {
            CullMode.None => DxCullMode.None,
            CullMode.Front => DxCullMode.Front,
            CullMode.Back => DxCullMode.Back,
            _ => DxCullMode.None
        };
    }

    public static DxStencilOp DirectX12(StencilOp stencilOp)
    {
        return stencilOp switch
        {
            StencilOp.Keep => DxStencilOp.Keep,
            StencilOp.Zero => DxStencilOp.Zero,
            StencilOp.Replace => DxStencilOp.Replace,
            StencilOp.IncrementAndClamp => DxStencilOp.IncrSat,
            StencilOp.DecrementAndClamp => DxStencilOp.DecrSat,
            StencilOp.Invert => DxStencilOp.Invert,
            StencilOp.IncrementAndWrap => DxStencilOp.Incr,
            StencilOp.DecrementAndWrap => DxStencilOp.Decr,
            _ => DxStencilOp.Keep
        };
    }

    public static DxBlend DirectX12(Blend blend)
    {
        return blend switch
        {
            Blend.Zero => DxBlend.Zero,
            Blend.One => DxBlend.One,
            Blend.SrcAlpha => DxBlend.SrcAlpha,
            Blend.InverseSrcAlpha => DxBlend.InvSrcAlpha,
            Blend.DestAlpha => DxBlend.DestAlpha,
            Blend.InverseDestAlpha => DxBlend.InvDestAlpha,
            Blend.SrcColor => DxBlend.SrcColor,
            Blend.InverseSrcColor => DxBlend.InvSrcColor,
            Blend.DestColor => DxBlend.DestColor,
            Blend.InverseDestColor => DxBlend.InvDestColor,
            Blend.BlendFactor => DxBlend.BlendFactor,
            Blend.InverseBlendFactor => DxBlend.InvBlendFactor,
            _ => DxBlend.Zero
        };
    }

    public static DxBlendOp DirectX12(BlendOp blendOp)
    {
        return blendOp switch
        {
            BlendOp.Add => DxBlendOp.Add,
            BlendOp.Subtract => DxBlendOp.Subtract,
            BlendOp.ReverseSubtract => DxBlendOp.RevSubtract,
            BlendOp.Min => DxBlendOp.Min,
            BlendOp.Max => DxBlendOp.Max,
            _ => DxBlendOp.Add
        };
    }

    public static ColorWriteEnable DirectX12(ColorComponentFlags colorComponentFlags)
    {
        ColorWriteEnable result = ColorWriteEnable.None;

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Red))
        {
            result |= ColorWriteEnable.Red;
        }

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Green))
        {
            result |= ColorWriteEnable.Green;
        }

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Blue))
        {
            result |= ColorWriteEnable.Blue;
        }

        if (colorComponentFlags.HasFlag(ColorComponentFlags.Alpha))
        {
            result |= ColorWriteEnable.Alpha;
        }

        return result;
    }

    public static Format DirectX12(ElementFormat elementFormat)
    {
        return elementFormat switch
        {
            ElementFormat.UByte1 => Format.FormatR8Uint,
            ElementFormat.UByte2 => Format.FormatR8G8Uint,
            ElementFormat.UByte4 => Format.FormatR8G8B8A8Uint,

            ElementFormat.Byte1 => Format.FormatR8Sint,
            ElementFormat.Byte2 => Format.FormatR8G8Sint,
            ElementFormat.Byte4 => Format.FormatR8G8B8A8Sint,

            ElementFormat.UByte1Normalized => Format.FormatR8Unorm,
            ElementFormat.UByte2Normalized => Format.FormatR8G8Unorm,
            ElementFormat.UByte4Normalized => Format.FormatR8G8B8A8Unorm,

            ElementFormat.Byte1Normalized => Format.FormatR8SNorm,
            ElementFormat.Byte2Normalized => Format.FormatR8G8SNorm,
            ElementFormat.Byte4Normalized => Format.FormatR8G8B8A8SNorm,

            ElementFormat.UShort1 => Format.FormatR16Uint,
            ElementFormat.UShort2 => Format.FormatR16G16Uint,
            ElementFormat.UShort4 => Format.FormatR16G16B16A16Uint,

            ElementFormat.Short1 => Format.FormatR16Sint,
            ElementFormat.Short2 => Format.FormatR16G16Sint,
            ElementFormat.Short4 => Format.FormatR16G16B16A16Sint,

            ElementFormat.UShort1Normalized => Format.FormatR16Unorm,
            ElementFormat.UShort2Normalized => Format.FormatR16G16Unorm,
            ElementFormat.UShort4Normalized => Format.FormatR16G16B16A16Unorm,

            ElementFormat.Short1Normalized => Format.FormatR16SNorm,
            ElementFormat.Short2Normalized => Format.FormatR16G16SNorm,
            ElementFormat.Short4Normalized => Format.FormatR16G16B16A16SNorm,

            ElementFormat.Half1 => Format.FormatR16Float,
            ElementFormat.Half2 => Format.FormatR16G16Float,
            ElementFormat.Half4 => Format.FormatR16G16B16A16Float,

            ElementFormat.Float1 => Format.FormatR32Float,
            ElementFormat.Float2 => Format.FormatR32G32Float,
            ElementFormat.Float3 => Format.FormatR32G32B32Float,
            ElementFormat.Float4 => Format.FormatR32G32B32A32Float,

            ElementFormat.UInt1 => Format.FormatR32Uint,
            ElementFormat.UInt2 => Format.FormatR32G32Uint,
            ElementFormat.UInt3 => Format.FormatR32G32B32Uint,
            ElementFormat.UInt4 => Format.FormatR32G32B32A32Uint,

            ElementFormat.Int1 => Format.FormatR32Sint,
            ElementFormat.Int2 => Format.FormatR32G32Sint,
            ElementFormat.Int3 => Format.FormatR32G32B32Sint,
            ElementFormat.Int4 => Format.FormatR32G32B32A32Sint,

            _ => Format.FormatUnknown
        };
    }

    public static (PrimitiveTopologyType PrimitiveTopologyType, D3DPrimitiveTopology PrimitiveTopology) DirectX12(PrimitiveTopology primitiveTopology)
    {
        return
        (
            primitiveTopology switch
            {
                PrimitiveTopology.PointList => PrimitiveTopologyType.Point,

                PrimitiveTopology.LineList or
                PrimitiveTopology.LineStrip or
                PrimitiveTopology.LineListWithAdjacency or
                PrimitiveTopology.LineStripWithAdjacency => PrimitiveTopologyType.Line,

                PrimitiveTopology.TriangleList or
                PrimitiveTopology.TriangleStrip or
                PrimitiveTopology.TriangleListWithAdjacency or
                PrimitiveTopology.TriangleStripWithAdjacency => PrimitiveTopologyType.Triangle,

                >= PrimitiveTopology.PatchList => PrimitiveTopologyType.Patch,

                _ => PrimitiveTopologyType.Undefined
            },
            primitiveTopology switch
            {
                PrimitiveTopology.PointList => D3DPrimitiveTopology.D3DPrimitiveTopologyPointlist,
                PrimitiveTopology.LineList => D3DPrimitiveTopology.D3DPrimitiveTopologyLinelist,
                PrimitiveTopology.LineStrip => D3DPrimitiveTopology.D3DPrimitiveTopologyLinestrip,
                PrimitiveTopology.LineListWithAdjacency => D3DPrimitiveTopology.D3DPrimitiveTopologyLinelistAdj,
                PrimitiveTopology.LineStripWithAdjacency => D3DPrimitiveTopology.D3DPrimitiveTopologyLinestripAdj,
                PrimitiveTopology.TriangleList => D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist,
                PrimitiveTopology.TriangleStrip => D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglestrip,
                PrimitiveTopology.TriangleListWithAdjacency => D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelistAdj,
                PrimitiveTopology.TriangleStripWithAdjacency => D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglestripAdj,
                >= PrimitiveTopology.PatchList => D3DPrimitiveTopology.D3DPrimitiveTopology1ControlPointPatchlist + (PrimitiveTopology.PatchList - primitiveTopology),
                _ => D3DPrimitiveTopology.D3DPrimitiveTopologyUndefined
            }
        );
    }

    public static CommandListType DirectX12(CommandQueueType commandQueueType)
    {
        return commandQueueType switch
        {
            CommandQueueType.Graphics => CommandListType.Direct,
            CommandQueueType.Compute => CommandListType.Compute,
            CommandQueueType.Copy => CommandListType.Copy,
            _ => CommandListType.None
        };
    }

    public static Format DirectX12(IndexFormat indexFormat)
    {
        return indexFormat switch
        {
            IndexFormat.UInt16 => Format.FormatR16Uint,
            IndexFormat.UInt32 => Format.FormatR32Uint,
            _ => Format.FormatUnknown
        };
    }

    public static (QueryHeapType QueryHeapType, DxQueryType QueryType) DirectX12(QueryType queryType)
    {
        return
        (
            queryType switch
            {
                QueryType.Occlusion or
                QueryType.BinaryOcclusion => QueryHeapType.Occlusion,

                QueryType.Timestamp => QueryHeapType.Timestamp,

                _ => QueryHeapType.Occlusion
            },
            queryType switch
            {
                QueryType.Occlusion => DxQueryType.Occlusion,
                QueryType.BinaryOcclusion => DxQueryType.BinaryOcclusion,
                QueryType.Timestamp => DxQueryType.Timestamp,
                _ => DxQueryType.Occlusion
            }
        );
    }

    internal static RaytracingGeometryType DirectX12(RayTracingGeometryType type)
    {
        throw new NotImplementedException();
    }

    internal static RaytracingGeometryFlags DirectX12(RayTracingGeometryFlags flags)
    {
        throw new NotImplementedException();
    }

    internal static RaytracingInstanceFlags DirectX12(RayTracingInstanceFlags flags)
    {
        throw new NotImplementedException();
    }

    internal static RaytracingAccelerationStructureBuildFlags DirectX12(AccelerationStructureBuildFlags flags)
    {
        throw new NotImplementedException();
    }
}
