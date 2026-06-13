namespace Zenith.NET;

public struct TextureDesc
{
    public TextureType Type;

    public PixelFormat Format;

    public uint Width;

    public uint Height;

    public uint Depth;

    public uint MipLevels;

    public uint ArrayLayers;

    public SampleCount SampleCount;

    public TextureUsages Usages;

    public static TextureDesc Texture1D(PixelFormat format, uint width, uint mipLevels)
    {
        return new()
        {
            Type = TextureType.Texture1D,
            Format = format,
            Width = width,
            Height = 1,
            Depth = 1,
            MipLevels = mipLevels,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.CopyDst
        };
    }

    public static TextureDesc Texture1DArray(PixelFormat format, uint width, uint arrayLayers, uint mipLevels)
    {
        return new()
        {
            Type = TextureType.Texture1DArray,
            Format = format,
            Width = width,
            Height = 1,
            Depth = 1,
            MipLevels = mipLevels,
            ArrayLayers = arrayLayers,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.CopyDst
        };
    }

    public static TextureDesc Texture2D(PixelFormat format, uint width, uint height, uint mipLevels, SampleCount sampleCount)
    {
        return new()
        {
            Type = TextureType.Texture2D,
            Format = format,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = mipLevels,
            ArrayLayers = 1,
            SampleCount = sampleCount,
            Usages = TextureUsages.Sampled | TextureUsages.CopyDst
        };
    }

    public static TextureDesc Texture2DArray(PixelFormat format, uint width, uint height, uint arrayLayers, uint mipLevels, SampleCount sampleCount)
    {
        return new()
        {
            Type = TextureType.Texture2DArray,
            Format = format,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = mipLevels,
            ArrayLayers = arrayLayers,
            SampleCount = sampleCount,
            Usages = TextureUsages.Sampled | TextureUsages.CopyDst
        };
    }

    public static TextureDesc Texture3D(PixelFormat format, uint width, uint height, uint depth, uint mipLevels)
    {
        return new()
        {
            Type = TextureType.Texture3D,
            Format = format,
            Width = width,
            Height = height,
            Depth = depth,
            MipLevels = mipLevels,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.CopyDst
        };
    }

    public static TextureDesc TextureCube(PixelFormat format, uint size, uint mipLevels)
    {
        return new()
        {
            Type = TextureType.TextureCube,
            Format = format,
            Width = size,
            Height = size,
            Depth = 1,
            MipLevels = mipLevels,
            ArrayLayers = 6,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.CopyDst
        };
    }

    public static TextureDesc TextureCubeArray(PixelFormat format, uint size, uint cubeCount, uint mipLevels)
    {
        return new()
        {
            Type = TextureType.TextureCubeArray,
            Format = format,
            Width = size,
            Height = size,
            Depth = 1,
            MipLevels = mipLevels,
            ArrayLayers = cubeCount * 6,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.CopyDst
        };
    }

    public static TextureDesc ColorAttachment(PixelFormat format, uint width, uint height, uint mipLevels, SampleCount sampleCount)
    {
        return new()
        {
            Type = TextureType.Texture2D,
            Format = format,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = mipLevels,
            ArrayLayers = 1,
            SampleCount = sampleCount,
            Usages = TextureUsages.Sampled | TextureUsages.ColorAttachment
        };
    }

    public static TextureDesc DepthStencilAttachment(PixelFormat format, uint width, uint height, SampleCount sampleCount)
    {
        return new()
        {
            Type = TextureType.Texture2D,
            Format = format,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = sampleCount,
            Usages = TextureUsages.Sampled | TextureUsages.DepthStencilAttachment
        };
    }
}
