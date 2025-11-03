using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKBufferView : BufferView
{
    public VkBufferView BufferView;

    public VKBufferView(GraphicsContext context, BufferViewDesc desc) : base(context, desc)
    {
        BufferViewCreateInfo createInfo = new()
        {
            SType = StructureType.BufferViewCreateInfo,
            Buffer = Desc.Buffer.Vulkan().Buffer,
            Format = Format.Undefined,
            Offset = Desc.OffsetInBytes,
            Range = Desc.SizeInBytes
        };

        Context.Vk.CreateBufferView(Context.Device, &createInfo, null, (VkBufferView*)Unsafe.AsPointer(ref BufferView)).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.BufferView,
            ObjectHandle = BufferView.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.Vk.DestroyBufferView(Context.Device, BufferView, null);
    }
}
