namespace Zenith.NET;

internal class Downloader(GraphicsContext context) : DisposableObject
{
    private readonly Lock @lock = new();
    private readonly List<Lease> available = [];
    private readonly Dictionary<CommandBuffer, List<Lease>> borrowed = [];

    public Buffer Buffer(CommandBuffer commandBuffer, BufferData data)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!borrowed.TryGetValue(commandBuffer, out List<Lease>? leases))
        {
            borrowed[commandBuffer] = leases = [];
        }

        if (!(available.Where(item => item.HasCapacityFor(data.SizeInBytes)).MinBy(static item => item.Buffer.Desc.SizeInBytes) is Lease lease && available.Remove(lease)))
        {
            lease = new(context.CreateBuffer(new()
            {
                SizeInBytes = data.SizeInBytes,
                StrideInBytes = 1,
                Access = BufferAccess.CpuReadOnly
            }));
        }

        leases.Add(lease.Borrow(data));

        return lease.Buffer;
    }

    public void Release(CommandBuffer commandBuffer)
    {
        using Lock.Scope _ = @lock.EnterScope();

        CleanupExpiredLeases();

        if (borrowed.Remove(commandBuffer, out List<Lease>? leases))
        {
            foreach (Lease lease in leases)
            {
                available.Add(lease.Writeback().Renew());
            }
        }
    }

    protected override void Destroy()
    {
        foreach (CommandBuffer commandBuffer in borrowed.Keys.ToArray())
        {
            Release(commandBuffer);
        }

        foreach (Lease lease in available)
        {
            lease.Release();
        }
        available.Clear();
    }

    private void CleanupExpiredLeases()
    {
        available.RemoveAll(static item => item.TryExpire());
    }

    private class Lease(Buffer buffer)
    {
        private DateTime expirationTime = DateTime.UtcNow + TimeSpan.FromSeconds(120);

        private BufferData data;

        public Buffer Buffer { get; } = buffer;

        public bool HasCapacityFor(uint sizeInBytes)
        {
            return Buffer.Desc.SizeInBytes >= sizeInBytes;
        }

        public Lease Borrow(BufferData data)
        {
            this.data = data;

            return this;
        }

        public Lease Writeback()
        {
            Buffer.Download(0, data);

            return this;
        }

        public bool TryExpire()
        {
            if (DateTime.UtcNow >= expirationTime)
            {
                Release();

                return true;
            }

            return false;
        }

        public Lease Renew()
        {
            expirationTime = DateTime.UtcNow + TimeSpan.FromSeconds(120);

            data = default;

            return this;
        }

        public void Release()
        {
            Buffer.Dispose();
        }
    }
}
