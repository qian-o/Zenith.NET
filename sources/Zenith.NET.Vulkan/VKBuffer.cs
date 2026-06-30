using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKBuffer : Buffer
{
    public VkBuffer Buffer;

    public ulong DeviceAddress;

    public VKBuffer(VKGraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        BufferCreateInfo createInfo = CreateInfo(desc, (uint)context.QueueFamilyIndices.Length, (uint*)ZenithMarshal.AllocateAndFill(scope, [.. context.QueueFamilyIndices]));

        context.Vk.CreateBuffer(context.Device, &createInfo, default, out Buffer).Success();

        Heap = new(context, new()
        {
            SizeInBytes = desc.SizeInBytes,
            Residency = desc.Residency
        }, Buffer);

        BufferDeviceAddressInfo deviceAddressInfo = new()
        {
            SType = StructureType.BufferDeviceAddressInfo,
            Buffer = Buffer
        };

        DeviceAddress = context.Vk.GetBufferDeviceAddress(context.Device, &deviceAddressInfo);
    }

    public VKBuffer(VKGraphicsContext context, BufferDesc desc, VkBuffer buffer) : base(context, desc)
    {
        Buffer = buffer;

        BufferDeviceAddressInfo deviceAddressInfo = new()
        {
            SType = StructureType.BufferDeviceAddressInfo,
            Buffer = Buffer
        };

        DeviceAddress = context.Vk.GetBufferDeviceAddress(context.Device, &deviceAddressInfo);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKHeap? Heap { get; }

    public override ResourceHandle ConstantHandle { get; }

    public override ResourceHandle StorageReadOnlyHandle { get; }

    public override ResourceHandle StorageReadWriteHandle { get; }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    public override nint Map()
    {
        throw new NotImplementedException();
    }

    public override void Unmap()
    {
        throw new NotImplementedException();
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

        Heap?.Dispose();
    }

    public static BufferCreateInfo CreateInfo(BufferDesc desc, uint queueFamilyIndexCount, uint* pQueueFamilyIndices)
    {
        return new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = desc.SizeInBytes,
            Usage = VKFormats.Vulkan(desc.Usages),
            SharingMode = queueFamilyIndexCount is 1 ? SharingMode.Exclusive : SharingMode.Concurrent,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = pQueueFamilyIndices
        };
    }
}
