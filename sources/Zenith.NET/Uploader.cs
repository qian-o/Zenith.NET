namespace Zenith.NET;

internal class Uploader(GraphicsContext context) : DisposableObject
{
    private readonly Lock @lock = new();
    private readonly List<Lease> available = [];
    private readonly Dictionary<CommandBuffer, List<Lease>> borrowed = [];

    public Buffer Buffer(CommandBuffer commandBuffer, uint sizeInBytes)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!borrowed.TryGetValue(commandBuffer, out List<Lease>? leases))
        {
            borrowed[commandBuffer] = leases = [];
        }

        if (!(available.Where(item => item.HasCapacityFor(sizeInBytes)).MinBy(static item => item.Buffer.Desc.SizeInBytes) is Lease lease && available.Remove(lease)))
        {
            lease = new(context.CreateBuffer(new()
            {
                SizeInBytes = sizeInBytes,
                StrideInBytes = 1,
                Access = BufferAccess.CpuWriteOnly,
                Usages = BufferUsages.CopySrc
            }));
        }

        leases.Add(lease);

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
                available.Add(lease.Renew());
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

        public Buffer Buffer { get; } = buffer;

        public bool HasCapacityFor(uint sizeInBytes)
        {
            return Buffer.Desc.SizeInBytes >= sizeInBytes;
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

            return this;
        }

        public void Release()
        {
            Buffer.Dispose();
        }
    }
}
