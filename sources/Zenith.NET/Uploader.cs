namespace Zenith.NET;

internal class Uploader(GraphicsContext context) : DisposableObject
{
    private const uint BufferSizeInBytes = 4096;
    private const uint TextureWidth = 512;
    private const uint TextureHeight = 512;
    private const uint AvailableResources = 128;

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
                SizeInBytes = Math.Max(sizeInBytes, BufferSizeInBytes),
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
                Width = Math.Max(width, TextureWidth),
                Height = Math.Max(height, TextureHeight),
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
            for (int i = 0; i < buffers.Length; i++)
            {
                Buffer buffer = buffers[i];

                if (availableBuffers.Count >= AvailableResources || buffer.Desc.SizeInBytes > BufferSizeInBytes)
                {
                    buffer.Dispose();
                }
                else
                {
                    availableBuffers.Add(buffer);
                }
            }
        }

        if (usedTextures.Remove(commandBuffer, out Texture[]? textures))
        {
            for (int i = 0; i < textures.Length; i++)
            {
                Texture texture = textures[i];

                if (availableTextures.Count >= AvailableResources || texture.Desc.Width > TextureWidth || texture.Desc.Height > TextureHeight)
                {
                    texture.Dispose();
                }
                else
                {
                    availableTextures.Add(texture);
                }
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
