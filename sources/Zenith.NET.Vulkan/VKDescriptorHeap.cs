using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKDescriptorHeap(VKGraphicsContext context,
                                       VKBuffer buffer,
                                       ulong reservedRange,
                                       VKDescriptorRegion? bufferRegion,
                                       VKDescriptorRegion? imageRegion,
                                       VKDescriptorRegion? samplerRegion) : DisposableObject
{
    public nint HostAddress { get; } = buffer.Map();

    public ulong DeviceAddress => buffer.DeviceAddress;

    public ulong ReservedRange => reservedRange;

    public VKDescriptorToken Allocate(ResourceDescriptorInfoEXT info)
    {
        VKDescriptorRegion? region = info.Type switch
        {
            DescriptorType.UniformBuffer or DescriptorType.StorageBuffer => bufferRegion,
            DescriptorType.SampledImage or DescriptorType.StorageImage => imageRegion,
            _ => null
        };

        if (region is null)
        {
            return default;
        }

        VKDescriptorToken token = region.Allocate(HostAddress, out HostAddressRangeEXT target);

        context.DescriptorHeap?.WriteResourceDescriptors(context.Device, 1, &info, &target).Success();

        return token;
    }

    public VKDescriptorToken Allocate(SamplerCreateInfo info)
    {
        if (samplerRegion is null)
        {
            return default;
        }

        VKDescriptorToken token = samplerRegion.Allocate(HostAddress, out HostAddressRangeEXT target);

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

        ulong bufferOffset = ZenithHelper.Align(heapProperties.MinResourceHeapReservedRange, heapProperties.BufferDescriptorSize);
        uint bufferBaseIndex = (uint)(bufferOffset / heapProperties.BufferDescriptorSize);

        ulong imageOffset = ZenithHelper.Align(bufferOffset + (bufferCapacity * heapProperties.BufferDescriptorSize), heapProperties.ImageDescriptorSize);
        uint imageBaseIndex = (uint)(imageOffset / heapProperties.ImageDescriptorSize);

        VKBuffer buffer = new(context, new()
        {
            SizeInBytes = (uint)ZenithHelper.Align(imageOffset + (imageCapacity * heapProperties.ImageDescriptorSize), heapProperties.ResourceHeapAlignment),
            Residency = MemoryResidency.CpuWriteOnly
        }, BufferUsageFlags.DescriptorHeapBitExt());

        return new(context,
                   buffer,
                   heapProperties.MinResourceHeapReservedRange,
                   new(bufferBaseIndex, (nuint)heapProperties.BufferDescriptorSize),
                   new(imageBaseIndex, (nuint)heapProperties.ImageDescriptorSize),
                   null);
    }

    public static VKDescriptorHeap CreateSamplerHeap(VKGraphicsContext context, uint samplerCapacity)
    {
        PhysicalDeviceProperties2 properties2 = new() { SType = StructureType.PhysicalDeviceProperties2 };
        properties2.AddNext(out PhysicalDeviceDescriptorHeapPropertiesEXT heapProperties);

        context.Vk.GetPhysicalDeviceProperties2(context.PhysicalDevice, &properties2);

        ulong samplerOffset = ZenithHelper.Align(heapProperties.MinSamplerHeapReservedRange, heapProperties.SamplerDescriptorSize);
        uint samplerBaseIndex = (uint)(samplerOffset / heapProperties.SamplerDescriptorSize);

        VKBuffer buffer = new(context, new()
        {
            SizeInBytes = (uint)ZenithHelper.Align(samplerOffset + (samplerCapacity * heapProperties.SamplerDescriptorSize), heapProperties.SamplerHeapAlignment),
            Residency = MemoryResidency.CpuWriteOnly
        }, BufferUsageFlags.DescriptorHeapBitExt());

        return new(context,
                   buffer,
                   heapProperties.MinSamplerHeapReservedRange,
                   null,
                   null,
                   new(samplerBaseIndex, (nuint)heapProperties.SamplerDescriptorSize));
    }
}
