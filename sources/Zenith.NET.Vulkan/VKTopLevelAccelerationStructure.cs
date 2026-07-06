using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public AccelerationStructureKHR AccelerationStructure;

    public ulong DeviceAddress;

    public VKDescriptorToken Token;

    public VKTopLevelAccelerationStructure(VKGraphicsContext context, VKCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        Instance = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(AccelerationStructureInstanceKHR) * desc.Instances.Length),
            Residency = MemoryResidency.CpuWriteOnly
        });

        AccelerationStructureBuildGeometryInfoKHR info = Info(scope, desc, out uint* maxPrimitiveCounts, out AccelerationStructureBuildRangeInfoKHR* buildRangeInfos);

        AccelerationStructureBuildSizesInfoKHR sizeInfo = new() { SType = StructureType.AccelerationStructureBuildSizesInfoKhr };
        context.AccelerationStructure?.GetAccelerationStructureBuildSizes(context.Device, AccelerationStructureBuildTypeKHR.DeviceKhr, &info, maxPrimitiveCounts, &sizeInfo);

        Storage = new(context, new()
        {
            SizeInBytes = (uint)sizeInfo.AccelerationStructureSize,
            Residency = MemoryResidency.GpuOnly
        }, BufferUsageFlags.AccelerationStructureStorageBitKhr);

        Scratch = new(context, new()
        {
            SizeInBytes = (uint)Math.Max(sizeInfo.BuildScratchSize, sizeInfo.UpdateScratchSize),
            Usages = BufferUsages.StorageReadWrite,
            Residency = MemoryResidency.GpuOnly
        });

        AccelerationStructureCreateInfoKHR createInfo = new()
        {
            SType = StructureType.AccelerationStructureCreateInfoKhr,
            Buffer = Storage.Buffer,
            Size = sizeInfo.AccelerationStructureSize,
            Type = AccelerationStructureTypeKHR.TopLevelKhr
        };

        context.AccelerationStructure?.CreateAccelerationStructure(context.Device, &createInfo, default, out AccelerationStructure).Success();

        AccelerationStructureDeviceAddressInfoKHR deviceAddressInfo = new()
        {
            SType = StructureType.AccelerationStructureDeviceAddressInfoKhr,
            AccelerationStructure = AccelerationStructure
        };

        DeviceAddress = context.AccelerationStructure?.GetAccelerationStructureDeviceAddress(context.Device, &deviceAddressInfo) ?? 0;

        info.DstAccelerationStructure = AccelerationStructure;
        info.ScratchData = new() { DeviceAddress = Scratch.DeviceAddress };

        BuildSyncBarrier(commandBuffer, PipelineStageFlags2.AccelerationStructureBuildBitKhr);
        context.AccelerationStructure?.CmdBuildAccelerationStructures(commandBuffer.CommandBuffer, 1, &info, &buildRangeInfos);
        BuildSyncBarrier(commandBuffer, PipelineStageFlags2.FragmentShaderBit | PipelineStageFlags2.ComputeShaderBit);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKBuffer Instance { get; }

    public VKBuffer Storage { get; }

    public VKBuffer Scratch { get; }

    public override ResourceHandle Handle => Token.ResourceHandle;

    public void Update(VKCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
        using ZenithMarshal.Scope scope = new();

        AccelerationStructureBuildGeometryInfoKHR info = Info(scope, newDesc, out _, out AccelerationStructureBuildRangeInfoKHR* buildRangeInfos);
        info.Mode = BuildAccelerationStructureModeKHR.UpdateKhr;
        info.SrcAccelerationStructure = AccelerationStructure;
        info.DstAccelerationStructure = AccelerationStructure;
        info.ScratchData = new() { DeviceAddress = Scratch.DeviceAddress };

        BuildSyncBarrier(commandBuffer, PipelineStageFlags2.AccelerationStructureBuildBitKhr);
        Context.AccelerationStructure?.CmdBuildAccelerationStructures(commandBuffer.CommandBuffer, 1, &info, &buildRangeInfos);
        BuildSyncBarrier(commandBuffer, PipelineStageFlags2.FragmentShaderBit | PipelineStageFlags2.ComputeShaderBit);
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
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
        Token.Dispose();

        Context.AccelerationStructure?.DestroyAccelerationStructure(Context.Device, AccelerationStructure, default);

        Scratch.Dispose();
        Storage.Dispose();
        Instance.Dispose();
    }

    private AccelerationStructureBuildGeometryInfoKHR Info(ZenithMarshal.Scope scope, TopLevelAccelerationStructureDesc desc, out uint* maxPrimitiveCounts, out AccelerationStructureBuildRangeInfoKHR* buildRangeInfos)
    {
        throw new NotImplementedException();
    }

    private static void BuildSyncBarrier(VKCommandBuffer commandBuffer, PipelineStageFlags2 dstStage)
    {
        MemoryBarrier2 memoryBarrier = new()
        {
            SType = StructureType.MemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr,
            SrcAccessMask = AccessFlags2.AccelerationStructureWriteBitKhr,
            DstStageMask = dstStage,
            DstAccessMask = AccessFlags2.AccelerationStructureReadBitKhr
        };

        DependencyInfo dependencyInfo = new()
        {
            SType = StructureType.DependencyInfo,
            MemoryBarrierCount = 1,
            PMemoryBarriers = &memoryBarrier
        };

        commandBuffer.Context.Vk.CmdPipelineBarrier2(commandBuffer.CommandBuffer, &dependencyInfo);
    }
}
