namespace Zenith.NET.Metal;

internal class MTLBuffer : Buffer
{
    public MtlBuffer Buffer;

    public MTLBuffer(MTLGraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        context.Register(Buffer = context.Device.MakeBuffer(desc.SizeInBytes, MTLFormats.Metal(desc.Residency)));

        View = new(context, new()
        {
            Buffer = this,
            SizeInBytes = desc.SizeInBytes,
            StrideInBytes = desc.StrideInBytes
        });
    }

    public MTLBuffer(MTLGraphicsContext context, BufferDesc desc, MtlBuffer buffer) : base(context, desc)
    {
        context.Register(Buffer = buffer);

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
        return type switch
        {
            NativeObjectType.MTLBuffer => Buffer.NativePtr,
            _ => default
        };
    }

    public override nint Map()
    {
        return Buffer.Contents();
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
        Context.Unregister(Buffer);

        View.Dispose();
        Buffer.Dispose();
    }
}
