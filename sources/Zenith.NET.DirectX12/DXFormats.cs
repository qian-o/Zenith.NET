using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal static class DXFormats
{
    public static DxHeapType DirectX12(HeapType heapType)
    {
        return heapType switch
        {
            HeapType.GpuOnly => DxHeapType.Default,
            HeapType.CpuReadOnly => DxHeapType.Readback,
            HeapType.CpuWriteOnly => DxHeapType.Upload,
            _ => default
        };
    }

    public static ResourceFlags DirectX12(BufferUsages bufferUsages)
    {
        ResourceFlags result = ResourceFlags.None;

        if (bufferUsages.HasFlag(BufferUsages.StorageReadWrite))
        {
            result |= ResourceFlags.AllowUnorderedAccess;
        }

        if (bufferUsages.HasFlag(BufferUsages.AccelerationStructure))
        {
            result |= ResourceFlags.RaytracingAccelerationStructure;
        }

        return result;
    }

    public static DxHeapType DirectX12(BufferAccess bufferAccess)
    {
        return bufferAccess switch
        {
            BufferAccess.GpuOnly => DxHeapType.Default,
            BufferAccess.CpuReadOnly => DxHeapType.Readback,
            BufferAccess.CpuWriteOnly => DxHeapType.Upload,
            _ => default
        };
    }

    public static ResourceFlags DirectX12(TextureUsages textureUsages)
    {
        ResourceFlags result = ResourceFlags.None;

        if (!textureUsages.HasFlag(TextureUsages.Sampled))
        {
            result |= ResourceFlags.DenyShaderResource;
        }

        if (textureUsages.HasFlag(TextureUsages.Storage))
        {
            result |= ResourceFlags.AllowUnorderedAccess;
        }

        if (textureUsages.HasFlag(TextureUsages.ColorAttachment))
        {
            result |= ResourceFlags.AllowRenderTarget;
        }

        if (textureUsages.HasFlag(TextureUsages.DepthStencil))
        {
            result |= ResourceFlags.AllowDepthStencil;
        }

        return result;
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

            _ => default
        };
    }

    public static SampleDesc DirectX12(SampleCount sampleCount)
    {
        return sampleCount switch
        {
            SampleCount.Count1 => new(1, 0),
            SampleCount.Count2 => new(2, 0),
            SampleCount.Count4 => new(4, 0),
            SampleCount.Count8 => new(8, 0),
            SampleCount.Count16 => new(16, 0),
            SampleCount.Count32 => new(32, 0),
            _ => default
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

            PixelFormat.D16UNorm => Format.FormatD16Unorm,
            PixelFormat.D24UNormS8UInt => Format.FormatD24UnormS8Uint,
            PixelFormat.D32Float => Format.FormatD32Float,
            PixelFormat.D32FloatS8UInt => Format.FormatD32FloatS8X24Uint,

            PixelFormat.BC4UNorm => Format.FormatBC4Unorm,
            PixelFormat.BC4SNorm => Format.FormatBC4SNorm,

            PixelFormat.BC5UNorm => Format.FormatBC5Unorm,
            PixelFormat.BC5SNorm => Format.FormatBC5SNorm,

            PixelFormat.BC6HUFloat => Format.FormatBC6HUF16,
            PixelFormat.BC6HSFloat => Format.FormatBC6HSF16,

            PixelFormat.BC7UNorm => Format.FormatBC7Unorm,
            PixelFormat.BC7SRgb => Format.FormatBC7UnormSrgb,

            _ => default
        };
    }

    public static Filter DirectX12(FilterMode minFilter, FilterMode magFilter, FilterMode mipFilter, uint maxAnisotropy, CompareOp compareOp)
    {
        if (maxAnisotropy > 1)
        {
            return compareOp is CompareOp.Never ? Filter.Anisotropic : Filter.ComparisonAnisotropic;
        }

        return (minFilter, magFilter, mipFilter, compareOp) switch
        {
            (FilterMode.Point, FilterMode.Point, FilterMode.Point, CompareOp.Never) => Filter.MinMagMipPoint,
            (FilterMode.Point, FilterMode.Point, FilterMode.Linear, CompareOp.Never) => Filter.MinMagPointMipLinear,
            (FilterMode.Point, FilterMode.Linear, FilterMode.Point, CompareOp.Never) => Filter.MinPointMagLinearMipPoint,
            (FilterMode.Point, FilterMode.Linear, FilterMode.Linear, CompareOp.Never) => Filter.MinPointMagMipLinear,
            (FilterMode.Linear, FilterMode.Point, FilterMode.Point, CompareOp.Never) => Filter.MinLinearMagMipPoint,
            (FilterMode.Linear, FilterMode.Point, FilterMode.Linear, CompareOp.Never) => Filter.MinLinearMagPointMipLinear,
            (FilterMode.Linear, FilterMode.Linear, FilterMode.Point, CompareOp.Never) => Filter.MinMagLinearMipPoint,
            (FilterMode.Linear, FilterMode.Linear, FilterMode.Linear, CompareOp.Never) => Filter.MinMagMipLinear,

            (FilterMode.Point, FilterMode.Point, FilterMode.Point, _) => Filter.ComparisonMinMagMipPoint,
            (FilterMode.Point, FilterMode.Point, FilterMode.Linear, _) => Filter.ComparisonMinMagPointMipLinear,
            (FilterMode.Point, FilterMode.Linear, FilterMode.Point, _) => Filter.ComparisonMinPointMagLinearMipPoint,
            (FilterMode.Point, FilterMode.Linear, FilterMode.Linear, _) => Filter.ComparisonMinPointMagMipLinear,
            (FilterMode.Linear, FilterMode.Point, FilterMode.Point, _) => Filter.ComparisonMinLinearMagMipPoint,
            (FilterMode.Linear, FilterMode.Point, FilterMode.Linear, _) => Filter.ComparisonMinLinearMagPointMipLinear,
            (FilterMode.Linear, FilterMode.Linear, FilterMode.Point, _) => Filter.ComparisonMinMagLinearMipPoint,
            (FilterMode.Linear, FilterMode.Linear, FilterMode.Linear, _) => Filter.ComparisonMinMagMipLinear,

            _ => default
        };
    }

    public static TextureAddressMode DirectX12(AddressMode addressMode)
    {
        return addressMode switch
        {
            AddressMode.Wrap => TextureAddressMode.Wrap,
            AddressMode.Mirror => TextureAddressMode.Mirror,
            AddressMode.Clamp => TextureAddressMode.Clamp,
            AddressMode.Border => TextureAddressMode.Border,
            _ => default
        };
    }

    public static ComparisonFunc DirectX12(CompareOp compareOp)
    {
        return compareOp switch
        {
            CompareOp.Never => ComparisonFunc.Never,
            CompareOp.Less => ComparisonFunc.Less,
            CompareOp.Equal => ComparisonFunc.Equal,
            CompareOp.LessEqual => ComparisonFunc.LessEqual,
            CompareOp.Greater => ComparisonFunc.Greater,
            CompareOp.NotEqual => ComparisonFunc.NotEqual,
            CompareOp.GreaterEqual => ComparisonFunc.GreaterEqual,
            CompareOp.Always => ComparisonFunc.Always,
            _ => default
        };
    }

    public static (float R, float G, float B, float A) DirectX12(BorderColor borderColor)
    {
        return borderColor switch
        {
            BorderColor.TransparentBlack => (0.0f, 0.0f, 0.0f, 0.0f),
            BorderColor.OpaqueBlack => (0.0f, 0.0f, 0.0f, 1.0f),
            BorderColor.OpaqueWhite => (1.0f, 1.0f, 1.0f, 1.0f),
            _ => default
        };
    }

    public static PrimitiveTopologyType DirectX12(PrimitiveTopology primitiveTopology)
    {
        return primitiveTopology switch
        {
            PrimitiveTopology.PointList => PrimitiveTopologyType.Point,

            PrimitiveTopology.LineList or
            PrimitiveTopology.LineStrip => PrimitiveTopologyType.Line,

            PrimitiveTopology.TriangleList or
            PrimitiveTopology.TriangleStrip => PrimitiveTopologyType.Triangle,

            _ => default
        };
    }
}
