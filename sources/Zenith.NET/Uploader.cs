namespace Zenith.NET;

internal class Uploader(GraphicsContext context) : DisposableObject
{
    private readonly Lock @lock = new();
    private readonly List<ResourceLease<Buffer>> bufferPool = [];
    private readonly Dictionary<CommandBuffer, Buffer[]> buffersInUse = [];
    private readonly List<ResourceLease<Texture>> texturePool = [];
    private readonly Dictionary<CommandBuffer, Texture[]> texturesInUse = [];

    public Buffer Buffer(CommandBuffer commandBuffer, uint sizeInBytes)
    {
        using Lock.Scope _ = @lock.EnterScope();

        CleanupExpiredLeases();

        if (!buffersInUse.TryGetValue(commandBuffer, out Buffer[]? buffers))
        {
            buffersInUse[commandBuffer] = buffers = [];
        }

        Buffer buffer;
        if (bufferPool.FirstOrDefault(item => item.Resource.Desc.SizeInBytes >= sizeInBytes) is ResourceLease<Buffer> lease && bufferPool.Remove(lease))
        {
            buffer = lease.Resource;
        }
        else
        {
            buffer = context.CreateBuffer(new()
            {
                SizeInBytes = sizeInBytes,
                StrideInBytes = 1,
                Flags = BufferUsageFlags.Dynamic
            });
        }

        buffersInUse[commandBuffer] = [.. buffers, buffer];

        return buffer;
    }

    public Texture Texture(CommandBuffer commandBuffer, PixelFormat format, uint width, uint height)
    {
        using Lock.Scope _ = @lock.EnterScope();

        CleanupExpiredLeases();

        if (!texturesInUse.TryGetValue(commandBuffer, out Texture[]? textures))
        {
            texturesInUse[commandBuffer] = textures = [];
        }

        Texture texture;
        if (texturePool.FirstOrDefault(item => item.Resource.Desc.Format == format && item.Resource.Desc.Width >= width && item.Resource.Desc.Height >= height) is ResourceLease<Texture> lease && texturePool.Remove(lease))
        {
            texture = lease.Resource;
        }
        else
        {
            texture = context.CreateTexture(new()
            {
                Type = TextureType.Texture2D,
                Format = format,
                Width = width,
                Height = height,
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = SampleCount.Count1,
                Flags = TextureUsageFlags.Dynamic
            });
        }

        texturesInUse[commandBuffer] = [.. textures, texture];

        return texture;
    }

    public void Release(CommandBuffer commandBuffer)
    {
        using Lock.Scope _ = @lock.EnterScope();

        CleanupExpiredLeases();

        if (buffersInUse.Remove(commandBuffer, out Buffer[]? buffers))
        {
            foreach (Buffer buffer in buffers)
            {
                bufferPool.Add(new(buffer));
            }
        }

        if (texturesInUse.Remove(commandBuffer, out Texture[]? textures))
        {
            foreach (Texture texture in textures)
            {
                texturePool.Add(new(texture));
            }
        }
    }

    protected override void Destroy()
    {
        foreach (ResourceLease<Buffer> lease in bufferPool)
        {
            lease.Release();
        }
        bufferPool.Clear();

        foreach (Buffer[] buffers in buffersInUse.Values)
        {
            foreach (Buffer buffer in buffers)
            {
                buffer.Dispose();
            }
        }
        buffersInUse.Clear();

        foreach (ResourceLease<Texture> lease in texturePool)
        {
            lease.Release();
        }
        texturePool.Clear();

        foreach (Texture[] textures in texturesInUse.Values)
        {
            foreach (Texture texture in textures)
            {
                texture.Dispose();
            }
        }
        texturesInUse.Clear();
    }

    private void CleanupExpiredLeases()
    {
        bufferPool.RemoveAll(static item => item.TryExpire());
        texturePool.RemoveAll(static item => item.TryExpire());
    }

    private class ResourceLease<T>(T resource) where T : DisposableObject
    {
        private readonly DateTime expirationTime = DateTime.UtcNow + TimeSpan.FromSeconds(120);

        public T Resource { get; } = resource;

        public bool TryExpire()
        {
            if (DateTime.UtcNow >= expirationTime)
            {
                Release();

                return true;
            }

            return false;
        }

        public void Release()
        {
            Resource.Dispose();
        }
    }
}
