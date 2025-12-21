using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXDescriptorAllocator(DXGraphicsContext context, DescriptorHeapType type) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly List<DXDescriptorPool> available = [];

    public DXDescriptorToken Allocate(uint length)
    {
        using Lock.Scope _ = @lock.EnterScope();

        CpuDescriptorHandle handle = default;
        if (available.FirstOrDefault(item => item.TryAllocate(length, out handle)) is not DXDescriptorPool pool)
        {
            pool = new(context, type);
            pool.TryAllocate(length, out handle);
        }

        return new() { Pool = pool, Handle = handle, Length = length };
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        foreach (DXDescriptorPool pool in available)
        {
            pool.Dispose();
        }
        available.Clear();
    }
}
