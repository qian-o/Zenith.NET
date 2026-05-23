using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXBuffer : Buffer
{
    public ComPtr<ID3D12Resource> Resource;

    public DXBuffer(DXGraphicsContext context, BufferDesc desc) : base(context, desc)
    {
    }

    public DXBufferView View { get; }

    public override ResourceHandle UniformHandle => View.UniformHandle;

    public override ResourceHandle StorageReadOnlyHandle => View.StorageReadOnlyHandle;

    public override ResourceHandle StorageReadWriteHandle => View.StorageReadWriteHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    public override MappedMemory Map()
    {
        void* pointer;
        Resource.Map(0, (DxRange*)null, &pointer).Success();

        return new((nint)pointer, Desc.SizeInBytes);
    }

    public override void Unmap()
    {
        Resource.Unmap(0, (DxRange*)null);
    }

    protected override void SetResourceName(string name)
    {
        Resource.SetName(name).Success();
    }

    protected override void Destroy()
    {
        Resource.Dispose();
    }
}
