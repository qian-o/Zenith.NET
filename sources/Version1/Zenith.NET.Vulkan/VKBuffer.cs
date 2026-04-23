using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKBuffer : Buffer
{
    public VkBuffer Buffer;

    public ulong DeviceAddress;

    public VKBuffer(VKGraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        (SharingMode sharingMode, uint queueFamilyIndexCount, nint pQueueFamilyIndices) = context.GetSharingModeInfo(scope);

        BufferCreateInfo createInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = desc.SizeInBytes,
            Usage = VKFormats.Vulkan(desc.Flags).UsageFlags,
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = (uint*)pQueueFamilyIndices
        };

        context.Vk.CreateBuffer(context.Device, &createInfo, null, out Buffer).Success();

        DeviceMemory = new(context, this);

        BufferDeviceAddressInfo addressInfo = new()
        {
            SType = StructureType.BufferDeviceAddressInfo,
            Buffer = Buffer
        };

        DeviceAddress = context.Vk.GetBufferDeviceAddress(context.Device, &addressInfo);

        View = new(context, new()
        {
            Buffer = this,
            OffsetInBytes = 0,
            SizeInBytes = desc.SizeInBytes,
            StrideInBytes = desc.StrideInBytes
        });
    }

    public VKBuffer(VKGraphicsContext context, BufferDesc desc, VkBufferUsageFlags otherUsageFlags) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        (SharingMode sharingMode, uint queueFamilyIndexCount, nint pQueueFamilyIndices) = context.GetSharingModeInfo(scope);

        BufferCreateInfo createInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = desc.SizeInBytes,
            Usage = VKFormats.Vulkan(desc.Flags).UsageFlags | otherUsageFlags,
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = (uint*)pQueueFamilyIndices
        };

        context.Vk.CreateBuffer(context.Device, &createInfo, null, out Buffer).Success();

        DeviceMemory = new(context, this);

        BufferDeviceAddressInfo addressInfo = new()
        {
            SType = StructureType.BufferDeviceAddressInfo,
            Buffer = Buffer
        };

        DeviceAddress = context.Vk.GetBufferDeviceAddress(context.Device, &addressInfo);

        View = new(context, new()
        {
            Buffer = this,
            OffsetInBytes = 0,
            SizeInBytes = desc.SizeInBytes,
            StrideInBytes = desc.StrideInBytes
        });
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKDeviceMemory DeviceMemory { get; }

    public VKBufferView View { get; }

    public override MappedMemory Map()
    {
        void* pointer;
        Context.Vk.MapMemory(Context.Device, DeviceMemory.DeviceMemory, 0, Desc.SizeInBytes, 0, &pointer).Success();

        return new() { Pointer = (nint)pointer, SizeInBytes = Desc.SizeInBytes };
    }

    public override void Unmap()
    {
        Context.Vk.UnmapMemory(Context.Device, DeviceMemory.DeviceMemory);
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
        View.Dispose();

        Context.Vk.DestroyBuffer(Context.Device, Buffer, null);

        DeviceMemory.Dispose();
    }
}
