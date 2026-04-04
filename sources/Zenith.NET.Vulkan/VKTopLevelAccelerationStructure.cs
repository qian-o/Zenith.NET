using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public AccelerationStructureKHR AccelerationStructure;

    public ulong DeviceAddress;

    public VKTopLevelAccelerationStructure(VKGraphicsContext context, TopLevelAccelerationStructureDesc desc, VKCommandBuffer commandBuffer) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        InstanceBuffer = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(AccelerationStructureInstanceKHR) * desc.Instances.Length),
            StrideInBytes = (uint)sizeof(AccelerationStructureInstanceKHR),
            Flags = BufferUsageFlags.AccelerationStructure | BufferUsageFlags.MapWrite
        });

        FillInstanceBuffer(desc, out AccelerationStructureGeometryKHR geometry, out AccelerationStructureBuildRangeInfoKHR buildRangeInfo);

        AccelerationStructureBuildGeometryInfoKHR buildInfo = new()
        {
            SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
            Type = AccelerationStructureTypeKHR.TopLevelKhr,
            Flags = VKFormats.Vulkan(desc.Flags),
            Mode = BuildAccelerationStructureModeKHR.BuildKhr,
            GeometryCount = 1,
            PGeometries = &geometry
        };

        AccelerationStructureBuildSizesInfoKHR sizeInfo = new() { SType = StructureType.AccelerationStructureBuildSizesInfoKhr };

        context.AccelerationStructure?.GetAccelerationStructureBuildSizes(context.Device, AccelerationStructureBuildTypeKHR.DeviceKhr, &buildInfo, &buildRangeInfo.PrimitiveCount, &sizeInfo);

        BufferDesc accelerationStructureBufferDesc = new()
        {
            SizeInBytes = (uint)sizeInfo.AccelerationStructureSize,
            StrideInBytes = (uint)sizeInfo.AccelerationStructureSize
        };

        AccelerationStructureBuffer = new(context, accelerationStructureBufferDesc, VkBufferUsageFlags.AccelerationStructureStorageBitKhr);

        AccelerationStructureCreateInfoKHR createInfo = new()
        {
            SType = StructureType.AccelerationStructureCreateInfoKhr,
            Buffer = AccelerationStructureBuffer.Buffer,
            Size = sizeInfo.AccelerationStructureSize,
            Type = AccelerationStructureTypeKHR.TopLevelKhr
        };

        context.AccelerationStructure?.CreateAccelerationStructure(context.Device, &createInfo, null, out AccelerationStructure).Success();

        AccelerationStructureDeviceAddressInfoKHR addressInfo = new()
        {
            SType = StructureType.AccelerationStructureDeviceAddressInfoKhr,
            AccelerationStructure = AccelerationStructure
        };

        DeviceAddress = context.AccelerationStructure?.GetAccelerationStructureDeviceAddress(context.Device, &addressInfo) ?? 0;

        ScratchBuffer = new(context, new()
        {
            SizeInBytes = (uint)sizeInfo.BuildScratchSize,
            StrideInBytes = (uint)sizeInfo.BuildScratchSize,
            Flags = BufferUsageFlags.ShaderResource
        });

        buildInfo.DstAccelerationStructure = AccelerationStructure;
        buildInfo.ScratchData = new() { DeviceAddress = ScratchBuffer.DeviceAddress };

        AccelerationStructureBuildRangeInfoKHR* pBuildRangeInfo = &buildRangeInfo;

        context.AccelerationStructure?.CmdBuildAccelerationStructures(commandBuffer.CommandBuffer, 1, &buildInfo, &pBuildRangeInfo);

        MemoryBarrier barrier = new()
        {
            SType = StructureType.MemoryBarrier,
            SrcAccessMask = AccessFlags.AccelerationStructureWriteBitKhr,
            DstAccessMask = AccessFlags.AccelerationStructureReadBitKhr
        };

        context.Vk.CmdPipelineBarrier(commandBuffer.CommandBuffer,
                                      PipelineStageFlags.AccelerationStructureBuildBitKhr,
                                      PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.ComputeShaderBit,
                                      0,
                                      1,
                                      &barrier,
                                      0,
                                      null,
                                      0,
                                      null);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKBuffer InstanceBuffer { get; }

    public VKBuffer AccelerationStructureBuffer { get; }

    public VKBuffer ScratchBuffer { get; }

    public void Update(VKCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
        FillInstanceBuffer(newDesc, out AccelerationStructureGeometryKHR geometry, out AccelerationStructureBuildRangeInfoKHR buildRangeInfo);

        AccelerationStructureBuildGeometryInfoKHR buildInfo = new()
        {
            SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
            Type = AccelerationStructureTypeKHR.TopLevelKhr,
            Flags = VKFormats.Vulkan(newDesc.Flags),
            Mode = BuildAccelerationStructureModeKHR.UpdateKhr,
            SrcAccelerationStructure = AccelerationStructure,
            DstAccelerationStructure = AccelerationStructure,
            GeometryCount = 1,
            PGeometries = &geometry,
            ScratchData = new() { DeviceAddress = ScratchBuffer.DeviceAddress }
        };

        AccelerationStructureBuildRangeInfoKHR* pBuildRangeInfo = &buildRangeInfo;

        Context.AccelerationStructure?.CmdBuildAccelerationStructures(commandBuffer.CommandBuffer, 1, &buildInfo, &pBuildRangeInfo);

        MemoryBarrier barrier = new()
        {
            SType = StructureType.MemoryBarrier,
            SrcAccessMask = AccessFlags.AccelerationStructureWriteBitKhr,
            DstAccessMask = AccessFlags.AccelerationStructureReadBitKhr
        };

        Context.Vk.CmdPipelineBarrier(commandBuffer.CommandBuffer,
                                      PipelineStageFlags.AccelerationStructureBuildBitKhr,
                                      PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.ComputeShaderBit,
                                      0,
                                      1,
                                      &barrier,
                                      0,
                                      null,
                                      0,
                                      null);
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.AccelerationStructureKhr,
            ObjectHandle = AccelerationStructure.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.AccelerationStructure?.DestroyAccelerationStructure(Context.Device, AccelerationStructure, null);

        ScratchBuffer.Dispose();
        AccelerationStructureBuffer.Dispose();
        InstanceBuffer.Dispose();
    }

    private void FillInstanceBuffer(TopLevelAccelerationStructureDesc desc, out AccelerationStructureGeometryKHR geometry, out AccelerationStructureBuildRangeInfoKHR buildRangeInfo)
    {
        uint instanceCount = (uint)desc.Instances.Length;

        MappedMemory mappedMemory = InstanceBuffer.Map();

        AccelerationStructureInstanceKHR* instances = (AccelerationStructureInstanceKHR*)mappedMemory.Pointer;
        for (uint i = 0; i < instanceCount; i++)
        {
            RayTracingInstance instance = desc.Instances[i];

            instances[i] = new()
            {
                Transform = VKFormats.Vulkan(instance.Transform),
                InstanceCustomIndex = instance.ID,
                Mask = instance.Mask,
                Flags = VKFormats.Vulkan(instance.Flags),
                AccelerationStructureReference = instance.AccelerationStructure.Vulkan().DeviceAddress
            };
        }

        InstanceBuffer.Unmap();

        geometry = new()
        {
            SType = StructureType.AccelerationStructureGeometryKhr,
            GeometryType = GeometryTypeKHR.InstancesKhr,
            Geometry = new()
            {
                Instances = new()
                {
                    SType = StructureType.AccelerationStructureGeometryInstancesDataKhr,
                    Data = new() { DeviceAddress = InstanceBuffer.DeviceAddress }
                }
            }
        };

        buildRangeInfo = new() { PrimitiveCount = instanceCount };
    }
}
