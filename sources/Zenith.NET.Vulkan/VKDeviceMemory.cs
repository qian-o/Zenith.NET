using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKDeviceMemory : GraphicsResource
{
    public DeviceMemory DeviceMemory;

    public VKDeviceMemory(VKGraphicsContext context, VKBuffer buffer) : base(context)
    {
        BufferMemoryRequirementsInfo2 requirementsInfo2 = new()
        {
            SType = StructureType.BufferMemoryRequirementsInfo2,
            Buffer = buffer.Buffer
        };

        MemoryRequirements2 requirements2 = new()
        {
            SType = StructureType.MemoryRequirements2
        };

        requirements2.AddNext(out MemoryDedicatedRequirements dedicatedRequirements);

        context.Vk.GetBufferMemoryRequirements2(context.Device, &requirementsInfo2, &requirements2);

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements2.MemoryRequirements.Size,
            MemoryTypeIndex = context.FindMemoryTypeIndex(requirements2.MemoryRequirements.MemoryTypeBits, buffer.Desc.Flags.HasFlag(BufferUsageFlags.Dynamic) ? MemoryPropertyFlags.HostVisibleBit : MemoryPropertyFlags.DeviceLocalBit)
        };

        allocateInfo.AddNext(out MemoryAllocateFlagsInfo flagsInfo);
        flagsInfo.Flags = MemoryAllocateFlags.AddressBit;

        if (dedicatedRequirements.PrefersDedicatedAllocation || dedicatedRequirements.RequiresDedicatedAllocation)
        {
            allocateInfo.AddNext(out MemoryDedicatedAllocateInfo dedicatedAllocateInfo);
            dedicatedAllocateInfo.Buffer = buffer.Buffer;
        }

        context.Vk.AllocateMemory(context.Device, &allocateInfo, null, (DeviceMemory*)Unsafe.AsPointer(ref DeviceMemory)).Success();

        context.Vk.BindBufferMemory(context.Device, buffer.Buffer, DeviceMemory, 0).Success();
    }

    public VKDeviceMemory(VKGraphicsContext context, VKTexture texture) : base(context)
    {
        ImageMemoryRequirementsInfo2 requirementsInfo2 = new()
        {
            SType = StructureType.ImageMemoryRequirementsInfo2,
            Image = texture.Image
        };

        MemoryRequirements2 requirements2 = new()
        {
            SType = StructureType.MemoryRequirements2
        };

        requirements2.AddNext(out MemoryDedicatedRequirements dedicatedRequirements);

        context.Vk.GetImageMemoryRequirements2(context.Device, &requirementsInfo2, &requirements2);

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements2.MemoryRequirements.Size,
            MemoryTypeIndex = context.FindMemoryTypeIndex(requirements2.MemoryRequirements.MemoryTypeBits, texture.Desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? MemoryPropertyFlags.HostVisibleBit : MemoryPropertyFlags.DeviceLocalBit)
        };

        if (dedicatedRequirements.PrefersDedicatedAllocation || dedicatedRequirements.RequiresDedicatedAllocation)
        {
            allocateInfo.AddNext(out MemoryDedicatedAllocateInfo dedicatedAllocateInfo);
            dedicatedAllocateInfo.Image = texture.Image;
        }

        context.Vk.AllocateMemory(context.Device, &allocateInfo, null, (DeviceMemory*)Unsafe.AsPointer(ref DeviceMemory)).Success();

        context.Vk.BindImageMemory(context.Device, texture.Image, DeviceMemory, 0).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Context.Vk.FreeMemory(Context.Device, DeviceMemory, null);
    }
}
