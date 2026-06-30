using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal static class VKFormats
{
    public static BufferUsageFlags Vulkan(BufferUsages bufferUsages)
    {
        BufferUsageFlags result = BufferUsageFlags.ShaderDeviceAddressBit;

        if (bufferUsages.HasFlag(BufferUsages.Vertex))
        {
            result |= BufferUsageFlags.VertexBufferBit;
        }

        if (bufferUsages.HasFlag(BufferUsages.Index))
        {
            result |= BufferUsageFlags.IndexBufferBit;
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
        }

        if (bufferUsages.HasFlag(BufferUsages.CopySrc))
        {
            result |= BufferUsageFlags.TransferSrcBit;
        }

        if (bufferUsages.HasFlag(BufferUsages.CopyDst))
        {
            result |= BufferUsageFlags.TransferDstBit;
        }

        return result;
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

        if (textureUsages.HasFlag(TextureUsages.CopySrc))
        {
            result |= ImageUsageFlags.TransferSrcBit;
        }

        if (textureUsages.HasFlag(TextureUsages.CopyDst))
        {
            result |= ImageUsageFlags.TransferDstBit;
        }

        return result;
    }
}
