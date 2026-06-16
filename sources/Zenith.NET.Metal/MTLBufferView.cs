namespace Zenith.NET.Metal;

internal class MTLBufferView(MTLGraphicsContext context, BufferViewDesc desc) : BufferView(context, desc)
{
    public ResourceHandle Handle = (desc.Buffer.Metal().Buffer.GpuAddress.ToUInt64() + desc.OffsetInBytes).ToHandle();

    public override ResourceHandle ConstantHandle => Handle;

    public override ResourceHandle StorageReadOnlyHandle => Handle;

    public override ResourceHandle StorageReadWriteHandle => Handle;

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
