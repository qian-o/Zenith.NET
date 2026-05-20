namespace Zenith.NET;

public struct TextureViewDesc
{
    public Texture Texture;

    public TextureType Type;

    public PixelFormat Format;

    public TextureSubresourceRange Range;

    public static TextureViewDesc Texture1D(Texture texture, PixelFormat format, uint baseMipLevel, uint mipLevelCount)
    {
        return new()
        {
            Texture = texture,
            Type = TextureType.Texture1D,
            Format = format,
            Range = new()
            {
                BaseMipLevel = baseMipLevel,
                LevelCount = mipLevelCount,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };
    }

    public static TextureViewDesc Texture1DArray(Texture texture, PixelFormat format, uint baseArrayLayer, uint layerCount, uint baseMipLevel, uint mipLevelCount)
    {
        return new()
        {
            Texture = texture,
            Type = TextureType.Texture1DArray,
            Format = format,
            Range = new()
            {
                BaseMipLevel = baseMipLevel,
                LevelCount = mipLevelCount,
                BaseArrayLayer = baseArrayLayer,
                LayerCount = layerCount
            }
        };
    }

    public static TextureViewDesc Texture2D(Texture texture, PixelFormat format, uint baseMipLevel, uint mipLevelCount)
    {
        return new()
        {
            Texture = texture,
            Type = TextureType.Texture2D,
            Format = format,
            Range = new()
            {
                BaseMipLevel = baseMipLevel,
                LevelCount = mipLevelCount,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };
    }

    public static TextureViewDesc Texture2DArray(Texture texture, PixelFormat format, uint baseArrayLayer, uint layerCount, uint baseMipLevel, uint mipLevelCount)
    {
        return new()
        {
            Texture = texture,
            Type = TextureType.Texture2DArray,
            Format = format,
            Range = new()
            {
                BaseMipLevel = baseMipLevel,
                LevelCount = mipLevelCount,
                BaseArrayLayer = baseArrayLayer,
                LayerCount = layerCount
            }
        };
    }

    public static TextureViewDesc Texture3D(Texture texture, PixelFormat format, uint baseMipLevel, uint mipLevelCount)
    {
        return new()
        {
            Texture = texture,
            Type = TextureType.Texture3D,
            Format = format,
            Range = new()
            {
                BaseMipLevel = baseMipLevel,
                LevelCount = mipLevelCount,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };
    }

    public static TextureViewDesc TextureCube(Texture texture, PixelFormat format, uint baseCubeIndex, uint baseMipLevel, uint mipLevelCount)
    {
        return new()
        {
            Texture = texture,
            Type = TextureType.TextureCube,
            Format = format,
            Range = new()
            {
                BaseMipLevel = baseMipLevel,
                LevelCount = mipLevelCount,
                BaseArrayLayer = baseCubeIndex * 6,
                LayerCount = 6
            }
        };
    }

    public static TextureViewDesc TextureCubeArray(Texture texture, PixelFormat format, uint baseCubeIndex, uint cubeCount, uint baseMipLevel, uint mipLevelCount)
    {
        return new()
        {
            Texture = texture,
            Type = TextureType.TextureCubeArray,
            Format = format,
            Range = new()
            {
                BaseMipLevel = baseMipLevel,
                LevelCount = mipLevelCount,
                BaseArrayLayer = baseCubeIndex * 6,
                LayerCount = cubeCount * 6
            }
        };
    }
}
