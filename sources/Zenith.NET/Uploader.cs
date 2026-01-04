namespace Zenith.NET;

internal class Uploader(GraphicsContext context) : DisposableObject
{
    private readonly Lock @lock = new();
    private readonly List<ResourceLease<Buffer>> available = [];
    private readonly Dictionary<CommandBuffer, Buffer[]> used = [];

    public Buffer Buffer(CommandBuffer commandBuffer, uint sizeInBytes)
    {
        using Lock.Scope _ = @lock.EnterScope();

        CleanupExpiredLeases();

        if (!used.TryGetValue(commandBuffer, out Buffer[]? buffers))
        {
            used[commandBuffer] = buffers = [];
        }

        Buffer buffer;
        if (available.FirstOrDefault(item => item.Resource.Desc.SizeInBytes >= sizeInBytes) is ResourceLease<Buffer> lease && available.Remove(lease))
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

        used[commandBuffer] = [.. buffers, buffer];

        return buffer;
    }

    public void Release(CommandBuffer commandBuffer)
    {
        using Lock.Scope _ = @lock.EnterScope();

        CleanupExpiredLeases();

        if (used.Remove(commandBuffer, out Buffer[]? buffers))
        {
            foreach (Buffer buffer in buffers)
            {
                available.Add(new(buffer));
            }
        }
    }

    protected override void Destroy()
    {
        foreach (ResourceLease<Buffer> lease in available)
        {
            lease.Release();
        }
        available.Clear();

        foreach (Buffer[] buffers in used.Values)
        {
            foreach (Buffer buffer in buffers)
            {
                buffer.Dispose();
            }
        }
        used.Clear();
    }

    private void CleanupExpiredLeases()
    {
        available.RemoveAll(static item => item.TryExpire());
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
