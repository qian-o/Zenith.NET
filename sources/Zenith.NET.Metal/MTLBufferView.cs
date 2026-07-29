namespace Zenith.NET.Metal;

internal class MTLBufferView : BufferView
{
    public MTLBufferView(MTLGraphicsContext context, BufferViewDesc desc) : base(context, desc)
    {
        ConstantHandle = (Desc.Buffer.Metal().Buffer.GpuAddress.ToUInt64() + Desc.OffsetInBytes).ToHandle();
        StorageReadOnlyHandle = (Desc.Buffer.Metal().Buffer.GpuAddress.ToUInt64() + Desc.OffsetInBytes).ToHandle();
        StorageReadWriteHandle = (Desc.Buffer.Metal().Buffer.GpuAddress.ToUInt64() + Desc.OffsetInBytes).ToHandle();
    }

    public override ResourceHandle ConstantHandle { get; }

    public override ResourceHandle StorageReadOnlyHandle { get; }

    public override ResourceHandle StorageReadWriteHandle { get; }

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
