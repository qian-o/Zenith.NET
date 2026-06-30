using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKBuffer : Buffer
{
    private readonly bool ownsMemory;

    public VkBuffer Buffer;

    public DeviceMemory DeviceMemory;

    public ulong OffsetInBytes;

    public ulong DeviceAddress;

    public VKBuffer(VKGraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        ownsMemory = true;

        using ZenithMarshal.Scope scope = new();

        (SharingMode sharingMode, uint queueFamilyIndexCount, nint pQueueFamilyIndices) = context.GetSharingModeInfo(scope);

        BufferCreateInfo createInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = desc.SizeInBytes,
            Usage = VKFormats.Vulkan(desc.Usages),
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = (uint*)pQueueFamilyIndices
        };

        context.Vk.CreateBuffer(context.Device, &createInfo, default, out Buffer).Success();

        BufferMemoryRequirementsInfo2 requirementsInfo2 = new()
        {
            SType = StructureType.BufferMemoryRequirementsInfo2,
            Buffer = Buffer
        };

        MemoryRequirements2 requirements2 = new()
        {
            SType = StructureType.MemoryRequirements2
        };

        requirements2.AddNext(out MemoryDedicatedRequirements requirements);

        context.Vk.GetBufferMemoryRequirements2(context.Device, &requirementsInfo2, &requirements2);

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements2.MemoryRequirements.Size,
            MemoryTypeIndex = context.FindMemoryTypeIndex(requirements2.MemoryRequirements.MemoryTypeBits, desc.Residency)
        };

        if (requirements.PrefersDedicatedAllocation || requirements.RequiresDedicatedAllocation)
        {
            allocateInfo.AddNext(out MemoryDedicatedAllocateInfo dedicatedAllocateInfo);
            dedicatedAllocateInfo.Buffer = Buffer;
        }

        allocateInfo.AddNext(out MemoryAllocateFlagsInfo allocateFlagsInfo);
        allocateFlagsInfo.Flags = MemoryAllocateFlags.DeviceAddressBit;

        context.Vk.AllocateMemory(context.Device, &allocateInfo, default, out DeviceMemory).Success();
        context.Vk.BindBufferMemory(context.Device, Buffer, DeviceMemory, 0).Success();

        BufferDeviceAddressInfo deviceAddressInfo = new()
        {
            SType = StructureType.BufferDeviceAddressInfo,
            Buffer = Buffer
        };

        DeviceAddress = context.Vk.GetBufferDeviceAddress(context.Device, &deviceAddressInfo);
    }

    public VKBuffer(VKGraphicsContext context, BufferDesc desc, VkBuffer buffer, DeviceMemory deviceMemory, ulong offsetInBytes) : base(context, desc)
    {
        ownsMemory = false;

        Buffer = buffer;
        DeviceMemory = deviceMemory;
        OffsetInBytes = offsetInBytes;

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
        Context.Vk.MapMemory(Context.Device, DeviceMemory, OffsetInBytes, Desc.SizeInBytes, MemoryMapFlags.None, &pointer).Success();

        return (nint)pointer;
    }

    public override void Unmap()
    {
        Context.Vk.UnmapMemory(Context.Device, DeviceMemory);
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

        if (ownsMemory)
        {
            Context.Vk.FreeMemory(Context.Device, DeviceMemory, default);
        }
    }
}
