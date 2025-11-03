using Silk.NET.Vulkan;

namespace Zenith.NET;

internal class VKBufferView : BufferView
{
    public VKBufferView(GraphicsContext context, BufferViewDesc desc) : base(context, desc)
    {
        BufferInfo = new()
        {
            Buffer = Desc.Buffer.Vulkan().Buffer,
            Offset = Desc.OffsetInBytes,
            Range = Desc.SizeInBytes
        };
    }

    public DescriptorBufferInfo BufferInfo { get; }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
