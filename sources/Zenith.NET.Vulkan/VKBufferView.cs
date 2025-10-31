using Silk.NET.Vulkan;

namespace Zenith.NET;

internal class VKBufferView : BufferView
{
    public VKBufferView(GraphicsContext context, BufferViewDesc desc) : base(context, desc)
    {
        BufferInfo = new()
        {
            Buffer = desc.Buffer.Vulkan().Buffer,
            Offset = desc.OffsetInBytes,
            Range = desc.SizeInBytes
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
