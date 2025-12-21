using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal record struct DXDescriptorToken : IDisposable
{
    public DXDescriptorPool Pool;

    public CpuDescriptorHandle Handle;

    public uint Length;

    public readonly CpuDescriptorHandle this[uint index]
    {
        get
        {
            if (index >= Length)
            {
                return default;
            }

            return new(Handle.Ptr + (Pool.HandleSize * index));
        }
    }

    public readonly void Dispose()
    {
        if (Length is 0)
        {
            return;
        }

        Pool.Free(Handle, Length);
    }
}
