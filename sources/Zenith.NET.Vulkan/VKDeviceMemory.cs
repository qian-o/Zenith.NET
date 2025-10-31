using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKDeviceMemory : GraphicsResource
{
    public DeviceMemory DeviceMemory;

    public VKDeviceMemory(GraphicsContext context, VKBuffer buffer) : base(context)
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

        Context.Vk.GetBufferMemoryRequirements2(Context.Device, &requirementsInfo2, &requirements2);

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements2.MemoryRequirements.Size,
            MemoryTypeIndex = Context.FindMemoryTypeIndex(requirements2.MemoryRequirements.MemoryTypeBits, buffer.Desc.Flags.HasFlag(BufferUsageFlags.Dynamic) ? MemoryPropertyFlags.HostVisibleBit : MemoryPropertyFlags.DeviceLocalBit)
        };

        allocateInfo.AddNext(out MemoryAllocateFlagsInfo flagsInfo);
        flagsInfo.Flags = MemoryAllocateFlags.AddressBit;

        if (dedicatedRequirements.PrefersDedicatedAllocation || dedicatedRequirements.RequiresDedicatedAllocation)
        {
            allocateInfo.AddNext(out MemoryDedicatedAllocateInfo dedicatedAllocateInfo);
            dedicatedAllocateInfo.Buffer = buffer.Buffer;
        }

        Context.Vk.AllocateMemory(Context.Device, &allocateInfo, null, (DeviceMemory*)Unsafe.AsPointer(ref DeviceMemory)).Success();
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
