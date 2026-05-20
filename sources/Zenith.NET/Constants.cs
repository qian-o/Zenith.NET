namespace Zenith.NET;

internal class Constants(GraphicsContext context) : DisposableObject
{
    private static readonly TimeSpan LeaseLifetime = TimeSpan.FromSeconds(120);

    private readonly Lock @lock = new();
    private readonly List<Lease> available = [];
    private readonly Dictionary<CommandBuffer, List<Lease>> borrowed = [];

    public Buffer Buffer<T>(CommandBuffer commandBuffer, T data) where T : unmanaged, IConstantsLayout<T>
    {
        using Lock.Scope _ = @lock.EnterScope();

        uint sizeInBytes = SizeInBytes<T>();

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
                Usages = BufferUsages.Uniform
            }));
        }

        leases.Add(lease);

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
        return context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => T.DirectX12SizeInBytes,
            GraphicsApi.Metal => T.MetalSizeInBytes,
            GraphicsApi.Vulkan => T.VulkanSizeInBytes,
            _ => 0
        };
    }

    private void Write<T>(T data, Buffer buffer) where T : unmanaged, IConstantsLayout<T>
    {
        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                T.WriteDirectX12(data, buffer);
                break;

            case GraphicsApi.Metal:
                T.WriteMetal(data, buffer);
                break;

            case GraphicsApi.Vulkan:
                T.WriteVulkan(data, buffer);
                break;
        }
    }

    private void CleanupExpiredLeases()
    {
        available.RemoveAll(static item => item.TryExpire());
    }

    private class Lease(Buffer buffer)
    {
        private DateTime expirationTime = DateTime.UtcNow + LeaseLifetime;

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
            expirationTime = DateTime.UtcNow + LeaseLifetime;

            return this;
        }

        public void Release()
        {
            Buffer.Dispose();
        }
    }
}