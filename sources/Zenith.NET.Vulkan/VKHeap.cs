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

        allocateInfo.AddNext(out MemoryAllocateFlagsInfo allocateFlagsInfo);
        allocateFlagsInfo.Flags = MemoryAllocateFlags.DeviceAddressBit;

        context.Vk.AllocateMemory(context.Device, &allocateInfo, default, out DeviceMemory).Success();
    }

    public VKHeap(VKGraphicsContext context, HeapDesc desc, VkBuffer buffer) : base(context, desc)
    {
        BufferMemoryRequirementsInfo2 requirementsInfo2 = new()
        {
            SType = StructureType.BufferMemoryRequirementsInfo2,
            Buffer = buffer
        };

        MemoryRequirements2 requirements2 = new() { SType = StructureType.MemoryRequirements2 };
        requirements2.AddNext(out MemoryDedicatedRequirements dedicatedRequirements);

        context.Vk.GetBufferMemoryRequirements2(context.Device, &requirementsInfo2, &requirements2);

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements2.MemoryRequirements.Size,
            MemoryTypeIndex = context.FindMemoryTypeIndex(requirements2.MemoryRequirements.MemoryTypeBits, desc.Residency)
        };

        if (dedicatedRequirements.PrefersDedicatedAllocation || dedicatedRequirements.RequiresDedicatedAllocation)
        {
            allocateInfo.AddNext(out MemoryDedicatedAllocateInfo dedicatedAllocateInfo);
            dedicatedAllocateInfo.Buffer = buffer;
        }

        allocateInfo.AddNext(out MemoryAllocateFlagsInfo allocateFlagsInfo);
        allocateFlagsInfo.Flags = MemoryAllocateFlags.DeviceAddressBit;

        context.Vk.AllocateMemory(context.Device, &allocateInfo, default, out DeviceMemory).Success();
        context.Vk.BindBufferMemory(context.Device, buffer, DeviceMemory, 0).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override Buffer CreateBufferImpl(ulong offsetInBytes, BufferDesc desc)
    {
        using ZenithMarshal.Scope scope = new();

        BufferCreateInfo createInfo = VKBuffer.CreateInfo(desc, (uint)Context.QueueFamilyIndices.Length, (uint*)ZenithMarshal.AllocateAndFill(scope, [.. Context.QueueFamilyIndices]));

        Context.Vk.CreateBuffer(Context.Device, &createInfo, default, out VkBuffer buffer).Success();
        Context.Vk.BindBufferMemory(Context.Device, buffer, DeviceMemory, offsetInBytes).Success();

        return new VKBuffer(Context, desc, buffer);
    }

    protected override Texture CreateTextureImpl(ulong offsetInBytes, TextureDesc desc)
    {
        throw new NotImplementedException();
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
