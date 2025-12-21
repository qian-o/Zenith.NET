using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal record struct DXDescriptorToken : IDisposable
{
    public DXDescriptorPool Pool;

    public CpuDescriptorHandle Handle;

    public uint Length;

    public readonly void Dispose()
    {
        Pool.Free(Handle, Length);
    }
}
