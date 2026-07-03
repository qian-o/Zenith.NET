using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKDescriptorHeap(VKGraphicsContext context,
                                       VKBuffer buffer,
                                       ulong reservedBytes,
                                       VKDescriptorRegion? bufferRegion,
                                       VKDescriptorRegion? imageRegion,
                                       VKDescriptorRegion? samplerRegion) : DisposableObject
{
    private readonly nint pointer = buffer.Map();

    public DeviceAddressRangeEXT Range => new(buffer.DeviceAddress, buffer.Desc.SizeInBytes);

    public ulong ReservedBytes => reservedBytes;

    public VKDescriptorToken Allocate(ResourceDescriptorInfoEXT info)
    {
        VKDescriptorRegion? region = info.Type switch
        {
            DescriptorType.UniformBuffer or
            DescriptorType.StorageBuffer or
            DescriptorType.AccelerationStructureKhr => bufferRegion,

            DescriptorType.SampledImage or
            DescriptorType.StorageImage => imageRegion,

            _ => null
        };

        if (region is null)
        {
            return default;
        }

        VKDescriptorToken token = region.Allocate(pointer, out HostAddressRangeEXT target);

        context.DescriptorHeap?.WriteResourceDescriptors(context.Device, 1, &info, &target).Success();

        return token;
    }

    public VKDescriptorToken Allocate(SamplerCreateInfo info)
    {
        if (samplerRegion is null)
        {
            return default;
        }

        VKDescriptorToken token = samplerRegion.Allocate(pointer, out HostAddressRangeEXT target);

        context.DescriptorHeap?.WriteSamplerDescriptors(context.Device, 1, &info, &target).Success();

        return token;
    }

    protected override void Destroy()
    {
        buffer.Unmap();
        buffer.Dispose();
    }

    public static VKDescriptorHeap CreateResourceHeap(VKGraphicsContext context, uint bufferCapacity, uint imageCapacity)
    {
        PhysicalDeviceProperties2 properties2 = new() { SType = StructureType.PhysicalDeviceProperties2 };
        properties2.AddNext(out PhysicalDeviceDescriptorHeapPropertiesEXT heapProperties);

        context.Vk.GetPhysicalDeviceProperties2(context.PhysicalDevice, &properties2);

        ulong reservedBytes = heapProperties.MinResourceHeapReservedRange;

        ulong bufferStride = heapProperties.BufferDescriptorSize;
        ulong bufferOffset = ZenithHelper.Align(reservedBytes, bufferStride);

        ulong imageStride = heapProperties.ImageDescriptorSize;
        ulong imageOffset = ZenithHelper.Align(bufferOffset + (bufferCapacity * bufferStride), imageStride);

        VKBuffer buffer = new(context, new()
        {
            SizeInBytes = (uint)ZenithHelper.Align(imageOffset + (imageCapacity * imageStride), heapProperties.ResourceHeapAlignment),
            Residency = MemoryResidency.CpuWriteOnly
        }, BufferUsageFlags.DescriptorHeapBitExt());

        return new(context,
                   buffer,
                   reservedBytes,
                   new((uint)(bufferOffset / bufferStride), (uint)bufferStride),
                   new((uint)(imageOffset / imageStride), (uint)imageStride),
                   null);
    }

    public static VKDescriptorHeap CreateSamplerHeap(VKGraphicsContext context, uint samplerCapacity)
    {
        PhysicalDeviceProperties2 properties2 = new() { SType = StructureType.PhysicalDeviceProperties2 };
        properties2.AddNext(out PhysicalDeviceDescriptorHeapPropertiesEXT heapProperties);

        context.Vk.GetPhysicalDeviceProperties2(context.PhysicalDevice, &properties2);

        ulong reservedBytes = heapProperties.MinSamplerHeapReservedRange;

        ulong samplerStride = heapProperties.SamplerDescriptorSize;
        ulong samplerOffset = ZenithHelper.Align(reservedBytes, samplerStride);

        VKBuffer buffer = new(context, new()
        {
            SizeInBytes = (uint)ZenithHelper.Align(samplerOffset + (samplerCapacity * samplerStride), heapProperties.SamplerHeapAlignment),
            Residency = MemoryResidency.CpuWriteOnly
        }, BufferUsageFlags.DescriptorHeapBitExt());

        return new(context,
                   buffer,
                   reservedBytes,
                   null,
                   null,
                   new((uint)(samplerOffset / samplerStride), (uint)samplerStride));
    }
}
