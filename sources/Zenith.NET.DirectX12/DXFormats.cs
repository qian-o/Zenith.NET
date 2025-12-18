using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal static class DXFormats
{
    public static (ResourceFlags Flags, ResourceStates States) DirectX12(BufferUsageFlags bufferUsageFlags)
    {
        ResourceFlags flags = ResourceFlags.None;

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.AccelerationStructure))
        {
            flags |= ResourceFlags.RaytracingAccelerationStructure;
        }

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.UnorderedAccess))
        {
            flags |= ResourceFlags.AllowUnorderedAccess;
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

        if (bufferUsageFlags.HasFlag(BufferUsageFlags.Dynamic))
        {
            states = ResourceStates.GenericRead;
        }

        return (flags, states);
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

        if (textureUsageFlags.HasFlag(TextureUsageFlags.ShaderResource))
        {
            states |= ResourceStates.AllShaderResource;
        }

        if (textureUsageFlags.HasFlag(TextureUsageFlags.UnorderedAccess))
        {
            states |= ResourceStates.UnorderedAccess;
        }

        if (textureUsageFlags.HasFlag(TextureUsageFlags.Dynamic))
        {
            states |= ResourceStates.CopySource;
        }

        return (flags, states);
    }

    public static (DxFilter Filter, DxComparisonFunc ComparisonFunc) DirectX12(Filter filter, ComparisonFunc comparisonFunc)
    {
        throw new NotImplementedException();
    }

    public static TextureAddressMode DirectX12(AddressMode u)
    {
        throw new NotImplementedException();
    }
}
