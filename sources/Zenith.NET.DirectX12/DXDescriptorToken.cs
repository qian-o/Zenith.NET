using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal record struct DXDescriptorToken
{
    public DXDescriptorPool Pool;

    public CpuDescriptorHandle Handle;

    public readonly void Free()
    {
        Pool.Free(Handle);
    }
}
