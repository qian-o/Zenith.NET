namespace Zenith.NET;

internal class Uploader(GraphicsContext context) : DisposableObject
{
    private readonly Lock @lock = new();
    private readonly List<ResourceLease<Buffer>> bufferPool = [];
    private readonly Dictionary<CommandBuffer, Buffer[]> buffersInUse = [];

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
                Flags = BufferUsageFlags.MapWrite
            });
        }

        buffersInUse[commandBuffer] = [.. buffers, buffer];

        return buffer;
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
    }

    private void CleanupExpiredLeases()
    {
        bufferPool.RemoveAll(static item => item.TryExpire());
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
