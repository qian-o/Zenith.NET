namespace Zenith.NET;

internal class Uploader(GraphicsContext context) : DisposableObject
{
    private const uint MinBufferSizeInBytes = 4096;
    private const uint MinTextureWidth = 256;
    private const uint MinTextureHeight = 256;
    private const uint MinTextureDepth = 32;

    private readonly Lock @lock = new();
    private readonly List<Buffer> availableBuffer = [];
    private readonly Dictionary<CommandBuffer, Buffer[]> usedBuffer = [];
    private readonly List<Texture> availableTexture = [];
    private readonly Dictionary<CommandBuffer, Texture[]> usedTexture = [];

    public Buffer Buffer(CommandBuffer commandBuffer, uint sizeInBytes)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!usedBuffer.TryGetValue(commandBuffer, out Buffer[]? buffers))
        {
            usedBuffer[commandBuffer] = buffers = [];
        }

        if (availableBuffer.FirstOrDefault(item => item.Desc.SizeInBytes >= sizeInBytes) is not Buffer buffer || !availableBuffer.Remove(buffer))
        {
            buffer = context.CreateBuffer(new()
            {
                SizeInBytes = Math.Max(sizeInBytes, MinBufferSizeInBytes),
                StrideInBytes = 1,
                Flags = BufferUsageFlags.Dynamic
            });
        }

        usedBuffer[commandBuffer] = [.. buffers, buffer];

        return buffer;
    }

    public Texture Texture(CommandBuffer commandBuffer, PixelFormat format, uint width, uint height, uint depth)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!usedTexture.TryGetValue(commandBuffer, out Texture[]? textures))
        {
            usedTexture[commandBuffer] = textures = [];
        }

        if (availableTexture.FirstOrDefault(item => item.Desc.Format == format && item.Desc.Width >= width && item.Desc.Height >= height && item.Desc.Depth >= depth) is not Texture texture || !availableTexture.Remove(texture))
        {
            texture = context.CreateTexture(new()
            {
                Type = TextureType.Texture3D,
                Format = format,
                Width = Math.Max(width, MinTextureWidth),
                Height = Math.Max(height, MinTextureHeight),
                Depth = Math.Max(depth, MinTextureDepth),
                MipLevels = 1,
                SampleCount = SampleCount.Count1,
                Flags = TextureUsageFlags.Dynamic
            });
        }

        usedTexture[commandBuffer] = [.. textures, texture];

        return texture;
    }

    public void Release(CommandBuffer commandBuffer)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (usedBuffer.Remove(commandBuffer, out Buffer[]? buffers))
        {
            availableBuffer.AddRange(buffers);
        }

        if (usedTexture.Remove(commandBuffer, out Texture[]? textures))
        {
            availableTexture.AddRange(textures);
        }
    }

    protected override void Destroy()
    {
        foreach (Buffer buffer in availableBuffer)
        {
            buffer.Dispose();
        }
        availableBuffer.Clear();

        foreach (Buffer[] buffers in usedBuffer.Values)
        {
            foreach (Buffer buffer in buffers)
            {
                buffer.Dispose();
            }
        }
        usedBuffer.Clear();

        foreach (Texture texture in availableTexture)
        {
            texture.Dispose();
        }
        availableTexture.Clear();

        foreach (Texture[] textures in usedTexture.Values)
        {
            foreach (Texture texture in textures)
            {
                texture.Dispose();
            }
        }
        usedTexture.Clear();
    }
}
