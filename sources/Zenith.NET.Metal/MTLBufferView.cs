namespace Zenith.NET.Metal;

internal class MTLBufferView(MTLGraphicsContext context, BufferViewDesc desc) : BufferView(context, desc)
{
    public override ResourceHandle ConstantHandle => (Desc.Buffer.Metal().Buffer.GpuAddress.ToUInt64() + Desc.OffsetInBytes).ToResourceHandle();

    public override ResourceHandle StorageReadOnlyHandle => (Desc.Buffer.Metal().Buffer.GpuAddress.ToUInt64() + Desc.OffsetInBytes).ToResourceHandle();

    public override ResourceHandle StorageReadWriteHandle => (Desc.Buffer.Metal().Buffer.GpuAddress.ToUInt64() + Desc.OffsetInBytes).ToResourceHandle();

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
