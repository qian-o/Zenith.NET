using System.Numerics;

namespace Zenith.NET;

public static class ZenithHelper
{
    public static T Align<T>(T size, T alignment) where T : INumberBase<T>, IBitwiseOperators<T, T, T>
    {
        return (size + alignment - T.One) & ~(alignment - T.One);
    }

    public static uint MipLevels(uint width, uint height, uint depth)
    {
        return (uint)MathF.Floor(MathF.Log2(MathF.Max(MathF.Max(width, height), depth))) + 1;
    }

    public static void MipDimensions(uint width, uint height, uint depth, uint mipLevel, out uint mipWidth, out uint mipHeight, out uint mipDepth)
    {
        mipWidth = Math.Max(1, width >> (int)mipLevel);
        mipHeight = Math.Max(1, height >> (int)mipLevel);
        mipDepth = Math.Max(1, depth >> (int)mipLevel);
    }

    public static (uint BlockWidth, uint BlockHeight) BlockSize(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.BC4UNorm or
            PixelFormat.BC4SNorm or
            PixelFormat.BC5UNorm or
            PixelFormat.BC5SNorm or
            PixelFormat.BC6HUFloat or
            PixelFormat.BC6HSFloat or
            PixelFormat.BC7UNorm or
            PixelFormat.BC7SRgb => (4, 4),

            PixelFormat.ETC2UNorm or
            PixelFormat.ETC2SRgb or
            PixelFormat.ETC2A1UNorm or
            PixelFormat.ETC2A1SRgb or
            PixelFormat.ETC2A8UNorm or
            PixelFormat.ETC2A8SRgb => (4, 4),

            PixelFormat.ASTC4x4UNorm or
            PixelFormat.ASTC4x4SRgb or
            PixelFormat.ASTC4x4Float => (4, 4),

            PixelFormat.ASTC5x5UNorm or
            PixelFormat.ASTC5x5SRgb or
            PixelFormat.ASTC5x5Float => (5, 5),

            PixelFormat.ASTC6x6UNorm or
            PixelFormat.ASTC6x6SRgb or
            PixelFormat.ASTC6x6Float => (6, 6),

            PixelFormat.ASTC8x8UNorm or
            PixelFormat.ASTC8x8SRgb or
            PixelFormat.ASTC8x8Float => (8, 8),

            PixelFormat.ASTC10x10UNorm or
            PixelFormat.ASTC10x10SRgb or
            PixelFormat.ASTC10x10Float => (10, 10),

            PixelFormat.ASTC12x12UNorm or
            PixelFormat.ASTC12x12SRgb or
            PixelFormat.ASTC12x12Float => (12, 12),

            _ => (1, 1)
        };
    }

    public static uint SizeInBytes(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R8UNorm or
            PixelFormat.R8SNorm or
            PixelFormat.R8UInt or
            PixelFormat.R8SInt => 1,

            PixelFormat.R16UNorm or
            PixelFormat.R16SNorm or
            PixelFormat.R16UInt or
            PixelFormat.R16SInt or
            PixelFormat.R16Float => 2,

            PixelFormat.R32UInt or
            PixelFormat.R32SInt or
            PixelFormat.R32Float => 4,

            PixelFormat.R8G8UNorm or
            PixelFormat.R8G8SNorm or
            PixelFormat.R8G8UInt or
            PixelFormat.R8G8SInt => 2,

            PixelFormat.R16G16UNorm or
            PixelFormat.R16G16SNorm or
            PixelFormat.R16G16UInt or
            PixelFormat.R16G16SInt or
            PixelFormat.R16G16Float => 4,

            PixelFormat.R32G32UInt or
            PixelFormat.R32G32SInt or
            PixelFormat.R32G32Float => 8,

            PixelFormat.R32G32B32UInt or
            PixelFormat.R32G32B32SInt or
            PixelFormat.R32G32B32Float => 12,

            PixelFormat.R8G8B8A8UNorm or
            PixelFormat.R8G8B8A8SNorm or
            PixelFormat.R8G8B8A8UInt or
            PixelFormat.R8G8B8A8SInt or
            PixelFormat.R8G8B8A8SRgb => 4,

            PixelFormat.R16G16B16A16UNorm or
            PixelFormat.R16G16B16A16SNorm or
            PixelFormat.R16G16B16A16UInt or
            PixelFormat.R16G16B16A16SInt or
            PixelFormat.R16G16B16A16Float => 8,

            PixelFormat.R32G32B32A32UInt or
            PixelFormat.R32G32B32A32SInt or
            PixelFormat.R32G32B32A32Float => 16,

            PixelFormat.B8G8R8A8UNorm or
            PixelFormat.B8G8R8A8SRgb => 4,

            PixelFormat.D16UNorm => 2,

            PixelFormat.D24UNormS8UInt => 4,

            PixelFormat.D32Float => 4,

            PixelFormat.D32FloatS8UInt => 5,

            PixelFormat.BC4UNorm or
            PixelFormat.BC4SNorm => 8,

            PixelFormat.BC5UNorm or
            PixelFormat.BC5SNorm or
            PixelFormat.BC6HUFloat or
            PixelFormat.BC6HSFloat or
            PixelFormat.BC7UNorm or
            PixelFormat.BC7SRgb => 16,

            PixelFormat.ETC2UNorm or
            PixelFormat.ETC2SRgb or
            PixelFormat.ETC2A1UNorm or
            PixelFormat.ETC2A1SRgb => 8,

            PixelFormat.ETC2A8UNorm or
            PixelFormat.ETC2A8SRgb => 16,

            PixelFormat.ASTC4x4UNorm or
            PixelFormat.ASTC4x4SRgb or
            PixelFormat.ASTC4x4Float or
            PixelFormat.ASTC5x5UNorm or
            PixelFormat.ASTC5x5SRgb or
            PixelFormat.ASTC5x5Float or
            PixelFormat.ASTC6x6UNorm or
            PixelFormat.ASTC6x6SRgb or
            PixelFormat.ASTC6x6Float or
            PixelFormat.ASTC8x8UNorm or
            PixelFormat.ASTC8x8SRgb or
            PixelFormat.ASTC8x8Float or
            PixelFormat.ASTC10x10UNorm or
            PixelFormat.ASTC10x10SRgb or
            PixelFormat.ASTC10x10Float or
            PixelFormat.ASTC12x12UNorm or
            PixelFormat.ASTC12x12SRgb or
            PixelFormat.ASTC12x12Float => 16,

            _ => 0
        };
    }

    public static uint SizeInBytes(PixelFormat format, uint width, uint height)
    {
        (uint blockWidth, uint blockHeight) = BlockSize(format);

        uint blocksWide = (width + blockWidth - 1) / blockWidth;
        uint blocksHigh = (height + blockHeight - 1) / blockHeight;

        return blocksWide * blocksHigh * SizeInBytes(format);
    }

    public static uint SizeInBytes(ElementFormat format)
    {
        return format switch
        {
            ElementFormat.UByte1 or
            ElementFormat.Byte1 or
            ElementFormat.UByte1Normalized or
            ElementFormat.Byte1Normalized => 1,

            ElementFormat.UByte2 or
            ElementFormat.Byte2 or
            ElementFormat.UByte2Normalized or
            ElementFormat.Byte2Normalized or
            ElementFormat.UShort1 or
            ElementFormat.Short1 or
            ElementFormat.UShort1Normalized or
            ElementFormat.Short1Normalized or
            ElementFormat.Half1 => 2,

            ElementFormat.UByte4 or
            ElementFormat.Byte4 or
            ElementFormat.UByte4Normalized or
            ElementFormat.Byte4Normalized or
            ElementFormat.UShort2 or
            ElementFormat.Short2 or
            ElementFormat.UShort2Normalized or
            ElementFormat.Short2Normalized or
            ElementFormat.Half2 or
            ElementFormat.Float1 or
            ElementFormat.UInt1 or
            ElementFormat.Int1 => 4,

            ElementFormat.UShort4 or
            ElementFormat.Short4 or
            ElementFormat.UShort4Normalized or
            ElementFormat.Short4Normalized or
            ElementFormat.Half4 or
            ElementFormat.Float2 or
            ElementFormat.UInt2 or
            ElementFormat.Int2 => 8,

            ElementFormat.Float3 or
            ElementFormat.UInt3 or
            ElementFormat.Int3 => 12,

            ElementFormat.Float4 or
            ElementFormat.UInt4 or
            ElementFormat.Int4 => 16,

            _ => 0
        };
    }

    public static uint FaceCount(TextureDesc desc)
    {
        return desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray ? 6u : 1u;
    }

    public static uint FaceIndex(TextureDesc desc, TextureSlice slice)
    {
        return desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray ? slice.Face : 0u;
    }

    public static uint FlattenArrayLayerCount(TextureDesc desc)
    {
        return desc.ArrayLayers * FaceCount(desc);
    }

    public static uint FlattenArrayLayerIndex(TextureDesc desc, TextureSlice slice)
    {
        return (slice.ArrayLayer * FaceCount(desc)) + FaceIndex(desc, slice);
    }

    public static (uint FlattenArrayLayerIndex, uint FlattenArrayLayerCount) FlattenArrayLayerRange(TextureViewDesc desc)
    {
        return (desc.FirstArrayLayer * FaceCount(desc.Texture.Desc), desc.ArrayLayerCount * FaceCount(desc.Texture.Desc));
    }

    public static uint SubresourceCount(TextureDesc desc)
    {
        return desc.MipLevels * desc.ArrayLayers * FaceCount(desc);
    }

    public static uint SubresourceIndex(TextureDesc desc, TextureSlice slice)
    {
        return (slice.MipLevel * desc.ArrayLayers * FaceCount(desc)) + (slice.ArrayLayer * FaceCount(desc)) + FaceIndex(desc, slice);
    }

    public static uint SubresourceSizeInBytes(TextureDesc desc, TextureSlice slice)
    {
        MipDimensions(desc.Width, desc.Height, desc.Depth, slice.MipLevel, out uint mipWidth, out uint mipHeight, out uint mipDepth);

        return SizeInBytes(desc.Format, mipWidth, mipHeight) * mipDepth;
    }

    public static ShaderStageFlags[] GraphicShaderStages()
    {
        return
        [
            ShaderStageFlags.Vertex,
            ShaderStageFlags.Hull,
            ShaderStageFlags.Domain,
            ShaderStageFlags.Geometry,
            ShaderStageFlags.Pixel,
            ShaderStageFlags.Amplification,
            ShaderStageFlags.Mesh
        ];
    }
}
