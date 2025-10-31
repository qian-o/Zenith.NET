using System.Runtime.CompilerServices;
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

        if (desc.Flags.HasFlag(BufferUsageFlags.Vertex))
        {
            createInfo.Usage |= VkBufferUsageFlags.VertexBufferBit;
        }

        if (desc.Flags.HasFlag(BufferUsageFlags.Index))
        {
            createInfo.Usage |= VkBufferUsageFlags.IndexBufferBit;
        }

        if (desc.Flags.HasFlag(BufferUsageFlags.Indirect))
        {
            createInfo.Usage |= VkBufferUsageFlags.IndirectBufferBit;
        }

        if (desc.Flags.HasFlag(BufferUsageFlags.AccelerationStructure))
        {
            createInfo.Usage |= VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr;
        }

        if (desc.Flags.HasFlag(BufferUsageFlags.Constant))
        {
            createInfo.Usage |= VkBufferUsageFlags.UniformBufferBit;
        }

        if (desc.Flags.HasFlag(BufferUsageFlags.ShaderResource) || desc.Flags.HasFlag(BufferUsageFlags.UnorderedAccess))
        {
            createInfo.Usage |= VkBufferUsageFlags.StorageBufferBit;
        }

        Context.Vk.CreateBuffer(Context.Device, &createInfo, null, (VkBuffer*)Unsafe.AsPointer(ref Buffer)).Success();

        DeviceMemory = new(Context, this);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKDeviceMemory DeviceMemory { get; }

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

    public ulong GetDeviceAddress()
    {
        BufferDeviceAddressInfo addressInfo = new()
        {
            SType = StructureType.BufferDeviceAddressInfo,
            Buffer = Buffer
        };

        return Context.Vk.GetBufferDeviceAddress(Context.Device, &addressInfo);
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
        DeviceMemory.Dispose();

        Context.Vk.DestroyBuffer(Context.Device, Buffer, null);
    }
}
