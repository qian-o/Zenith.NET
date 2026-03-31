namespace Zenith.NET.Metal;

internal class MTLBufferView(MTLGraphicsContext context, BufferViewDesc desc) : BufferView(context, desc)
{
    public nuint GpuAddress = desc.Buffer.Metal().GpuAddress + desc.OffsetInBytes;

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
