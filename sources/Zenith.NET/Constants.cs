namespace Zenith.NET;

internal class Constants(GraphicsContext context) : DisposableObject
{
    private readonly Lock @lock = new();
    private readonly List<Lease> available = [];
    private readonly Dictionary<CommandBuffer, List<Lease>> borrowed = [];

    public Buffer Buffer<T>(CommandBuffer commandBuffer, T data) where T : unmanaged, IConstantsLayout<T>
    {
        uint sizeInBytes = SizeInBytes<T>();
        Lease lease;

        using (Lock.Scope _ = @lock.EnterScope())
        {
            if (!borrowed.TryGetValue(commandBuffer, out List<Lease>? leases))
            {
                borrowed[commandBuffer] = leases = [];
            }

            Lease? availableLease = available.Where(item => item.HasCapacityFor(sizeInBytes)).MinBy(static item => item.Buffer.Desc.SizeInBytes);
            if (availableLease is not null && available.Remove(availableLease))
            {
                lease = availableLease;
            }
            else
            {
                lease = new(context.CreateBuffer(new()
                {
                    SizeInBytes = sizeInBytes,
                    StrideInBytes = 1,
                    Access = BufferAccess.CpuWriteOnly,
                    Usages = BufferUsages.Uniform
                }));
            }

            leases.Add(lease);
        }

        Write(data, lease.Buffer);

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

    private uint SizeInBytes<T>() where T : unmanaged, IConstantsLayout<T>
    {
        return context.Api switch
        {
            GraphicsApi.DirectX12 => T.SizeInBytesOnDirectX12,
            GraphicsApi.Metal => T.SizeInBytesOnMetal,
            GraphicsApi.Vulkan => T.SizeInBytesOnVulkan,
            _ => 0
        };
    }

    private void Write<T>(T data, Buffer buffer) where T : unmanaged, IConstantsLayout<T>
    {
        switch (context.Api)
        {
            case GraphicsApi.DirectX12:
                T.DirectX12(data, buffer);
                break;

            case GraphicsApi.Metal:
                T.Metal(data, buffer);
                break;

            case GraphicsApi.Vulkan:
                T.Vulkan(data, buffer);
                break;
        }
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