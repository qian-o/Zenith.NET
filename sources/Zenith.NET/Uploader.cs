namespace Zenith.NET;

internal class Uploader(GraphicsContext context) : DisposableObject
{
    private readonly Lock @lock = new();
    private readonly List<BufferLease> leases = [];
    private readonly Dictionary<CommandBuffer, Buffer[]> borrowed = [];

    public Buffer Buffer(CommandBuffer commandBuffer, uint sizeInBytes)
    {
        using Lock.Scope _ = @lock.EnterScope();

        CleanupExpiredLeases();

        if (!borrowed.TryGetValue(commandBuffer, out Buffer[]? buffers))
        {
            borrowed[commandBuffer] = buffers = [];
        }

        Buffer buffer;
        if (leases.FirstOrDefault(item => item.Buffer.Desc.SizeInBytes >= sizeInBytes) is BufferLease lease && leases.Remove(lease))
        {
            buffer = lease.Buffer;
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

        borrowed[commandBuffer] = [.. buffers, buffer];

        return buffer;
    }

    public void Release(CommandBuffer commandBuffer)
    {
        using Lock.Scope _ = @lock.EnterScope();

        CleanupExpiredLeases();

        if (borrowed.Remove(commandBuffer, out Buffer[]? buffers))
        {
            foreach (Buffer buffer in buffers)
            {
                leases.Add(new(buffer));
            }
        }
    }

    protected override void Destroy()
    {
        foreach (BufferLease lease in leases)
        {
            lease.Release();
        }
        leases.Clear();

        foreach (Buffer[] buffers in borrowed.Values)
        {
            foreach (Buffer buffer in buffers)
            {
                buffer.Dispose();
            }
        }
        borrowed.Clear();
    }

    private void CleanupExpiredLeases()
    {
        leases.RemoveAll(static item => item.TryExpire());
    }

    private class BufferLease(Buffer buffer)
    {
        private readonly DateTime expirationTime = DateTime.UtcNow + TimeSpan.FromSeconds(120);

        public Buffer Buffer { get; } = buffer;

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
            Buffer.Dispose();
        }
    }
}
