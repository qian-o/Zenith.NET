namespace Zenith.NET.Metal;

internal class MTLBufferView(MTLGraphicsContext context, BufferViewDesc desc) : BufferView(context, desc)
{
    public ResourceHandle ResourceHandle = (desc.Buffer.Metal().GpuAddress + desc.OffsetInBytes).ToResourceHandle();

    public override ResourceHandle ConstantHandle => ResourceHandle;

    public override ResourceHandle StorageReadOnlyHandle => ResourceHandle;

    public override ResourceHandle StorageReadWriteHandle => ResourceHandle;

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
