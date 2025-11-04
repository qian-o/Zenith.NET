using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKBuffer : Buffer
{
    public VkBuffer Buffer;

    public ulong DeviceAddress;

    public VKBuffer(VKGraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        uint* queueFamilyIndices = (uint*)ZenithMarshal.Allocate<uint>(scope, (uint)context.QueueFamilyIndices.Length);
        context.QueueFamilyIndices.CopyTo(new Span<uint>(queueFamilyIndices, context.QueueFamilyIndices.Length));

        BufferCreateInfo createInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = desc.SizeInBytes,
            Usage = VKFormats.Vulkan(desc.Flags),
            SharingMode = context.QueueFamilyIndices.Length is 1 ? SharingMode.Exclusive : SharingMode.Concurrent,
            QueueFamilyIndexCount = (uint)context.QueueFamilyIndices.Length,
            PQueueFamilyIndices = queueFamilyIndices
        };

        context.Vk.CreateBuffer(context.Device, &createInfo, null, (VkBuffer*)Unsafe.AsPointer(ref Buffer)).Success();

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

        return new()
        {
            Pointer = (nint)pointer,
            SizeInBytes = Desc.SizeInBytes,
            RowPitch = Desc.SizeInBytes,
            SlicePitch = Desc.SizeInBytes
        };
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

        DeviceMemory.Dispose();

        Context.Vk.DestroyBuffer(Context.Device, Buffer, null);
    }
}
