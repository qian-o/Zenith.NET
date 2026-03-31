namespace Zenith.NET.Metal;

internal class MTLBuffer : Buffer
{
    public MtlBuffer Buffer;

    public nuint GpuAddress;

    public MTLBuffer(MTLGraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        Heap = new(context, desc, out Buffer);

        GpuAddress = Buffer.GpuAddress;
    }

    public MTLHeap Heap { get; }

    public override MappedMemory Map()
    {
        return new() { Pointer = Buffer.Contents(), SizeInBytes = Desc.SizeInBytes };
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
        Buffer.Dispose();

        Heap.Dispose();
    }
}
