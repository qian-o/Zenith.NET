using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKHeap : Heap
{
    public DeviceMemory DeviceMemory;

    public VKHeap(VKGraphicsContext context, HeapDesc desc) : base(context, desc)
    {
        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = desc.SizeInBytes,
            MemoryTypeIndex = context.FindMemoryTypeIndex(uint.MaxValue, desc.Residency)
        };

        allocateInfo.AddNext(out MemoryAllocateFlagsInfo flagsInfo);
        flagsInfo.Flags = MemoryAllocateFlags.DeviceAddressBit;

        context.Vk.AllocateMemory(context.Device, &allocateInfo, default, out DeviceMemory).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override Buffer CreateBufferImpl(ulong offsetInBytes, BufferDesc desc)
    {
        BufferCreateInfo createInfo = VKBuffer.CreateInfo(desc, Context.Capabilities, Context.QueueFamilies);

        Context.Vk.CreateBuffer(Context.Device, &createInfo, default, out VkBuffer buffer).Success();
        Context.Vk.BindBufferMemory(Context.Device, buffer, DeviceMemory, offsetInBytes).Success();

        return new VKBuffer(Context, desc, buffer, new(DeviceMemory, offsetInBytes, true, false));
    }

    protected override Texture CreateTextureImpl(ulong offsetInBytes, TextureDesc desc)
    {
        ImageCreateInfo createInfo = VKTexture.CreateInfo(desc, Context.QueueFamilies);

        Context.Vk.CreateImage(Context.Device, &createInfo, default, out Image image).Success();
        Context.Vk.BindImageMemory(Context.Device, image, DeviceMemory, offsetInBytes).Success();

        return new VKTexture(Context, desc, image, new(DeviceMemory, offsetInBytes, true, false));
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.DeviceMemory,
            ObjectHandle = DeviceMemory.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.Vk.FreeMemory(Context.Device, DeviceMemory, default);
    }
}
