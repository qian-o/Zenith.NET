using Silk.NET.Vulkan;

namespace Zenith.NET;

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
        VkBufferUsageFlags result = VkBufferUsageFlags.TransferSrcBit | VkBufferUsageFlags.TransferDstBit | VkBufferUsageFlags.ShaderDeviceAddressBit;

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

        if (textureUsageFlags.HasFlag(TextureUsageFlags.ShaderResource))
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

    public static (VkFilter MagFilter, VkFilter MinFilter, SamplerMipmapMode MipmapMode) Vulkan(Filter filter)
    {
        VkFilter magFilter = VkFilter.Nearest;
        VkFilter minFilter = VkFilter.Nearest;
        SamplerMipmapMode mipmapMode = SamplerMipmapMode.Nearest;

        switch (filter)
        {
            case Filter.MinPointMagPointMipPoint:
                magFilter = VkFilter.Nearest;
                minFilter = VkFilter.Nearest;
                mipmapMode = SamplerMipmapMode.Nearest;
                break;

            case Filter.MinPointMagPointMipLinear:
                magFilter = VkFilter.Nearest;
                minFilter = VkFilter.Nearest;
                mipmapMode = SamplerMipmapMode.Linear;
                break;

            case Filter.MinPointMagLinearMipPoint:
                magFilter = VkFilter.Linear;
                minFilter = VkFilter.Nearest;
                mipmapMode = SamplerMipmapMode.Nearest;
                break;

            case Filter.MinPointMagLinearMipLinear:
                magFilter = VkFilter.Linear;
                minFilter = VkFilter.Nearest;
                mipmapMode = SamplerMipmapMode.Linear;
                break;

            case Filter.MinLinearMagPointMipPoint:
                magFilter = VkFilter.Nearest;
                minFilter = VkFilter.Linear;
                mipmapMode = SamplerMipmapMode.Nearest;
                break;

            case Filter.MinLinearMagPointMipLinear:
                magFilter = VkFilter.Nearest;
                minFilter = VkFilter.Linear;
                mipmapMode = SamplerMipmapMode.Linear;
                break;

            case Filter.MinLinearMagLinearMipPoint:
                magFilter = VkFilter.Linear;
                minFilter = VkFilter.Linear;
                mipmapMode = SamplerMipmapMode.Nearest;
                break;

            case Filter.MinLinearMagLinearMipLinear:
                magFilter = VkFilter.Linear;
                minFilter = VkFilter.Linear;
                mipmapMode = SamplerMipmapMode.Linear;
                break;

            case Filter.Anisotropic:
                magFilter = VkFilter.Linear;
                minFilter = VkFilter.Linear;
                mipmapMode = SamplerMipmapMode.Linear;
                break;
        }

        return (magFilter, minFilter, mipmapMode);
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
        throw new NotImplementedException();
    }

    public static VkBorderColor Vulkan(BorderColor borderColor)
    {
        throw new NotImplementedException();
    }
}
