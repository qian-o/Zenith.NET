using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKBuffer : Buffer
{
    public VkBuffer Buffer;

    public VKBuffer(GraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        uint* queueFamilyIndices = (uint*)ZenithMarshal.Allocate<uint>(scope, (uint)Context.QueueFamilyIndices.Length);
        Context.QueueFamilyIndices.CopyTo(new Span<uint>(queueFamilyIndices, Context.QueueFamilyIndices.Length));

        BufferCreateInfo createInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = desc.SizeInBytes,
            Usage = VkBufferUsageFlags.TransferSrcBit
                    | VkBufferUsageFlags.TransferDstBit
                    | VkBufferUsageFlags.ShaderDeviceAddressBit,
            SharingMode = Context.QueueFamilyIndices.Length is 1 ? SharingMode.Exclusive : SharingMode.Concurrent,
            QueueFamilyIndexCount = (uint)Context.QueueFamilyIndices.Length,
            PQueueFamilyIndices = queueFamilyIndices
        };
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override MappedMemory Map()
    {
        throw new NotImplementedException();
    }

    public override void Unmap()
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        throw new NotImplementedException();
    }
}
