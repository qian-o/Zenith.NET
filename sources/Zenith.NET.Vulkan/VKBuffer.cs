using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKBuffer : Buffer
{
    public VkBuffer Buffer;

    public VKAllocation Allocation;

    public ulong DeviceAddress;

    public VKBuffer(VKGraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        BufferCreateInfo createInfo = CreateInfo(desc, context.QueueFamilies);

        context.Vk.CreateBuffer(context.Device, &createInfo, default, out Buffer).Success();

        BufferMemoryRequirementsInfo2 requirementsInfo2 = new()
        {
            SType = StructureType.BufferMemoryRequirementsInfo2,
            Buffer = Buffer
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
            dedicatedAllocateInfo.Buffer = Buffer;
        }

        allocateInfo.AddNext(out MemoryAllocateFlagsInfo allocateFlagsInfo);
        allocateFlagsInfo.Flags = MemoryAllocateFlags.DeviceAddressBit;

        context.Vk.AllocateMemory(context.Device, &allocateInfo, default, out DeviceMemory deviceMemory).Success();
        context.Vk.BindBufferMemory(context.Device, Buffer, deviceMemory, 0).Success();

        Allocation = new(deviceMemory, 0, true);

        BufferDeviceAddressInfo deviceAddressInfo = new()
        {
            SType = StructureType.BufferDeviceAddressInfo,
            Buffer = Buffer
        };

        DeviceAddress = context.Vk.GetBufferDeviceAddress(context.Device, &deviceAddressInfo);
    }

    public VKBuffer(VKGraphicsContext context, BufferDesc desc, BufferUsageFlags usage) : base(context, desc)
    {
        BufferCreateInfo createInfo = CreateInfo(desc, context.QueueFamilies);
        createInfo.Usage |= usage;

        context.Vk.CreateBuffer(context.Device, &createInfo, default, out Buffer).Success();

        BufferMemoryRequirementsInfo2 requirementsInfo2 = new()
        {
            SType = StructureType.BufferMemoryRequirementsInfo2,
            Buffer = Buffer
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
            dedicatedAllocateInfo.Buffer = Buffer;
        }

        allocateInfo.AddNext(out MemoryAllocateFlagsInfo allocateFlagsInfo);
        allocateFlagsInfo.Flags = MemoryAllocateFlags.DeviceAddressBit;

        context.Vk.AllocateMemory(context.Device, &allocateInfo, default, out DeviceMemory deviceMemory).Success();
        context.Vk.BindBufferMemory(context.Device, Buffer, deviceMemory, 0).Success();

        Allocation = new(deviceMemory, 0, true);

        BufferDeviceAddressInfo deviceAddressInfo = new()
        {
            SType = StructureType.BufferDeviceAddressInfo,
            Buffer = Buffer
        };

        DeviceAddress = context.Vk.GetBufferDeviceAddress(context.Device, &deviceAddressInfo);
    }

    public VKBuffer(VKGraphicsContext context, BufferDesc desc, VkBuffer buffer, VKAllocation allocation) : base(context, desc)
    {
        Buffer = buffer;
        Allocation = allocation;

        BufferDeviceAddressInfo deviceAddressInfo = new()
        {
            SType = StructureType.BufferDeviceAddressInfo,
            Buffer = Buffer
        };

        DeviceAddress = context.Vk.GetBufferDeviceAddress(context.Device, &deviceAddressInfo);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override ResourceHandle ConstantHandle { get; }

    public override ResourceHandle StorageReadOnlyHandle { get; }

    public override ResourceHandle StorageReadWriteHandle { get; }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    public override nint Map()
    {
        void* pointer;
        Context.Vk.MapMemory(Context.Device, Allocation.DeviceMemory, Allocation.OffsetInBytes, Desc.SizeInBytes, MemoryMapFlags.None, &pointer).Success();

        return (nint)pointer;
    }

    public override void Unmap()
    {
        Context.Vk.UnmapMemory(Context.Device, Allocation.DeviceMemory);
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.Buffer,
            ObjectHandle = Buffer.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.Vk.DestroyBuffer(Context.Device, Buffer, default);

        if (Allocation.IsOwned)
        {
            Context.Vk.FreeMemory(Context.Device, Allocation.DeviceMemory, default);
        }
    }

    public static BufferCreateInfo CreateInfo(BufferDesc desc, QueueFamilies queueFamilies)
    {
        return new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = desc.SizeInBytes,
            Usage = VKFormats.Vulkan(desc.Usages),
            SharingMode = queueFamilies.SharingMode,
            QueueFamilyIndexCount = queueFamilies.IndexCount,
            PQueueFamilyIndices = queueFamilies.Indices
        };
    }
}
