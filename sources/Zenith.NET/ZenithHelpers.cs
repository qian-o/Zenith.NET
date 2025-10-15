using System.Numerics;

namespace Zenith.NET;

public static class ZenithHelpers
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
            PixelFormat.R8G8B8A8UNormSRgb or
            PixelFormat.R8G8B8A8SNorm or
            PixelFormat.R8G8B8A8UInt or
            PixelFormat.R8G8B8A8SInt => 4,

            PixelFormat.R16G16B16A16UNorm or
            PixelFormat.R16G16B16A16SNorm or
            PixelFormat.R16G16B16A16UInt or
            PixelFormat.R16G16B16A16SInt or
            PixelFormat.R16G16B16A16Float => 8,

            PixelFormat.R32G32B32A32UInt or
            PixelFormat.R32G32B32A32SInt or
            PixelFormat.R32G32B32A32Float => 16,

            PixelFormat.B8G8R8A8UNorm or
            PixelFormat.B8G8R8A8UNormSRgb => 4,

            PixelFormat.D24UNormS8UInt => 4,

            PixelFormat.D32FloatS8UInt => 5,

            PixelFormat.BC1UNorm or
            PixelFormat.BC1UNormSRgb => 8,

            PixelFormat.BC2UNorm or
            PixelFormat.BC2UNormSRgb or
            PixelFormat.BC3UNorm or
            PixelFormat.BC3UNormSRgb => 16,

            PixelFormat.BC4UNorm or
            PixelFormat.BC4SNorm => 8,

            PixelFormat.BC5UNorm or
            PixelFormat.BC5SNorm or
            PixelFormat.BC7UNorm or
            PixelFormat.BC7UNormSRgb => 16,

            _ => 0
        };
    }

    public static bool IsCompressed(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.BC1UNorm or
            PixelFormat.BC1UNormSRgb or
            PixelFormat.BC2UNorm or
            PixelFormat.BC2UNormSRgb or
            PixelFormat.BC3UNorm or
            PixelFormat.BC3UNormSRgb or
            PixelFormat.BC4UNorm or
            PixelFormat.BC4SNorm or
            PixelFormat.BC5UNorm or
            PixelFormat.BC5SNorm or
            PixelFormat.BC7UNorm or
            PixelFormat.BC7UNormSRgb => true,

            _ => false
        };
    }

    public static uint RowPitch(uint width, PixelFormat format)
    {
        return IsCompressed(format) ? (width + 3) / 4 * SizeInBytes(format) : width * SizeInBytes(format);
    }

    public static uint NumRows(uint height, PixelFormat format)
    {
        return IsCompressed(format) ? (height + 3) / 4 : height;
    }

    public static uint SlicePitch(uint width, uint height, PixelFormat format)
    {
        return RowPitch(width, format) * NumRows(height, format);
    }

    public static bool IsTexture1D(TextureType type)
    {
        return type is TextureType.Texture1D or TextureType.Texture1DArray;
    }

    public static bool IsTexture2D(TextureType type)
    {
        return type is TextureType.Texture2D or TextureType.Texture2DArray or TextureType.TextureCube or TextureType.TextureCubeArray;
    }

    public static bool IsTexture3D(TextureType type)
    {
        return type is TextureType.Texture3D;
    }

    public static bool IsTextureCube(TextureType type)
    {
        return type is TextureType.TextureCube or TextureType.TextureCubeArray;
    }

    public static bool IsTextureArray(TextureType type)
    {
        return type is TextureType.Texture1DArray or TextureType.Texture2DArray or TextureType.TextureCubeArray;
    }

    public static uint SubresourceCount(TextureDesc desc)
    {
        uint facesPerMip = IsTextureCube(desc.Type) ? 6u : 1u;

        return desc.Layers * desc.MipLevels * facesPerMip;
    }

    public static uint SubresourceIndex(TextureDesc desc, TextureSlice slice)
    {
        uint facesPerMip = IsTextureCube(desc.Type) ? 6u : 1u;

        return (slice.Layer * desc.MipLevels * facesPerMip) + (slice.MipLevel * facesPerMip) + slice.Face;
    }

    public static uint SubresourceSizeInBytes(TextureDesc desc, TextureSlice slice)
    {
        MipDimensions(desc.Width, desc.Height, desc.Depth, slice.MipLevel, out uint mipWidth, out uint mipHeight, out uint mipDepth);

        return SlicePitch(mipWidth, mipHeight, desc.Format) * mipDepth;
    }
}
