namespace Zenith.NET.Metal;

internal class MTLBuffer : Buffer
{
    public MtlBuffer Buffer;

    public MTLBuffer(MTLGraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        Buffer = context.Device.MakeBuffer(desc.SizeInBytes, MTLFormats.Metal(desc.Residency));

        context.AddResidency(Buffer);

        View = new(context, new()
        {
            Buffer = this,
            SizeInBytes = desc.SizeInBytes,
            StrideInBytes = desc.StrideInBytes
        });
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public MTLBufferView View { get; }

    public override ResourceHandle ConstantHandle => View.ConstantHandle;

    public override ResourceHandle StorageReadOnlyHandle => View.StorageReadOnlyHandle;

    public override ResourceHandle StorageReadWriteHandle => View.StorageReadWriteHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    public override MappedMemory Map()
    {
        return new(Buffer.Contents(), Desc.SizeInBytes);
    }

    public override void Unmap()
    {
    }

    protected override void SetResourceName(string name)
    {
        Buffer.Label = name;
    }

    protected override void Destroy()
    {
        Context.RemoveResidency(Buffer);

        View.Dispose();
        Buffer.Dispose();
    }
}
