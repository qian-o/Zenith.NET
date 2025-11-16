namespace Zenith.NET;

internal class Uploader(GraphicsContext context) : DisposableObject
{
    private const uint MinBufferSizeInBytes = 4096;
    private const uint MaxBufferSizeInBytes = 8192;
    private const uint MinTextureWidth = 512;
    private const uint MaxTextureWidth = 1024;
    private const uint MinTextureHeight = 512;
    private const uint MaxTextureHeight = 1024;
    private const uint MaxAvailableResources = 256;

    private readonly Lock @lock = new();
    private readonly List<Buffer> availableBuffers = [];
    private readonly Dictionary<CommandBuffer, Buffer[]> usedBuffers = [];
    private readonly List<Texture> availableTextures = [];
    private readonly Dictionary<CommandBuffer, Texture[]> usedTextures = [];

    public Buffer Buffer(CommandBuffer commandBuffer, uint sizeInBytes)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!usedBuffers.TryGetValue(commandBuffer, out Buffer[]? buffers))
        {
            usedBuffers[commandBuffer] = buffers = [];
        }

        if (availableBuffers.FirstOrDefault(item => item.Desc.SizeInBytes >= sizeInBytes) is not Buffer buffer || !availableBuffers.Remove(buffer))
        {
            buffer = context.CreateBuffer(new()
            {
                SizeInBytes = Math.Max(sizeInBytes, MinBufferSizeInBytes),
                StrideInBytes = 1,
                Flags = BufferUsageFlags.Dynamic
            });
        }

        usedBuffers[commandBuffer] = [.. buffers, buffer];

        return buffer;
    }

    public Texture Texture(CommandBuffer commandBuffer, PixelFormat format, uint width, uint height)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!usedTextures.TryGetValue(commandBuffer, out Texture[]? textures))
        {
            usedTextures[commandBuffer] = textures = [];
        }

        if (availableTextures.FirstOrDefault(item => item.Desc.Format == format && item.Desc.Width >= width && item.Desc.Height >= height) is not Texture texture || !availableTextures.Remove(texture))
        {
            texture = context.CreateTexture(new()
            {
                Type = TextureType.Texture2D,
                Format = format,
                Width = Math.Max(width, MinTextureWidth),
                Height = Math.Max(height, MinTextureHeight),
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = SampleCount.Count1,
                Flags = TextureUsageFlags.Dynamic
            });
        }

        usedTextures[commandBuffer] = [.. textures, texture];

        return texture;
    }

    public void Release(CommandBuffer commandBuffer)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (usedBuffers.Remove(commandBuffer, out Buffer[]? buffers))
        {
            availableBuffers.AddRange(buffers);

            for (int i = availableBuffers.Count - 1; i >= 0; --i)
            {
                Buffer buffer = availableBuffers[i];

                if (buffer.Desc.SizeInBytes > MaxBufferSizeInBytes)
                {
                    buffer.Dispose();

                    availableBuffers.RemoveAt(i);
                }
            }

            while (availableBuffers.Count > MaxAvailableResources)
            {
                availableBuffers[^1].Dispose();

                availableBuffers.RemoveAt(availableBuffers.Count - 1);
            }
        }

        if (usedTextures.Remove(commandBuffer, out Texture[]? textures))
        {
            availableTextures.AddRange(textures);

            for (int i = availableTextures.Count - 1; i >= 0; --i)
            {
                Texture texture = availableTextures[i];

                if (texture.Desc.Width > MaxTextureWidth || texture.Desc.Height > MaxTextureHeight)
                {
                    texture.Dispose();

                    availableTextures.RemoveAt(i);
                }
            }

            while (availableTextures.Count > MaxAvailableResources)
            {
                availableTextures[^1].Dispose();

                availableTextures.RemoveAt(availableTextures.Count - 1);
            }
        }
    }

    protected override void Destroy()
    {
        foreach (Buffer buffer in availableBuffers)
        {
            buffer.Dispose();
        }
        availableBuffers.Clear();

        foreach (Buffer[] buffers in usedBuffers.Values)
        {
            foreach (Buffer buffer in buffers)
            {
                buffer.Dispose();
            }
        }
        usedBuffers.Clear();

        foreach (Texture texture in availableTextures)
        {
            texture.Dispose();
        }
        availableTextures.Clear();

        foreach (Texture[] textures in usedTextures.Values)
        {
            foreach (Texture texture in textures)
            {
                texture.Dispose();
            }
        }
        usedTextures.Clear();
    }
}
