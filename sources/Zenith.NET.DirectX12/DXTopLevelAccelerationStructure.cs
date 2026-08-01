using Silk.NET.Direct3D12;
using Silk.NET.Maths;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public DXDescriptorToken Token;

    public DXTopLevelAccelerationStructure(DXGraphicsContext context, DXCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc desc) : base(context, desc)
    {
        Instance = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(RaytracingInstanceDesc) * desc.Instances.Length),
            Residency = MemoryResidency.CpuWriteOnly
        });

        BuildRaytracingAccelerationStructureInputs inputs = Inputs(desc);

        RaytracingAccelerationStructurePrebuildInfo prebuildInfo = new();
        context.Device.GetRaytracingAccelerationStructurePrebuildInfo(&inputs, &prebuildInfo);

        AccelerationStructure = new(context, new()
        {
            SizeInBytes = (uint)prebuildInfo.ResultDataMaxSizeInBytes,
            Residency = MemoryResidency.GpuOnly
        }, ResourceFlags.RaytracingAccelerationStructure);

        Scratch = new(context, new()
        {
            SizeInBytes = (uint)prebuildInfo.ScratchDataSizeInBytes,
            Usages = BufferUsages.StorageReadWrite,
            Residency = MemoryResidency.GpuOnly
        });

        BuildRaytracingAccelerationStructureDesc buildDesc = new()
        {
            DestAccelerationStructureData = AccelerationStructure.GPUVirtualAddress,
            Inputs = inputs,
            ScratchAccelerationStructureData = Scratch.GPUVirtualAddress
        };

        BuildSyncBarrier(commandBuffer, BarrierSync.BuildRaytracingAccelerationStructure);
        commandBuffer.CommandList.BuildRaytracingAccelerationStructure(&buildDesc, 0, default(RaytracingAccelerationStructurePostbuildInfoDesc*));
        BuildSyncBarrier(commandBuffer, BarrierSync.AllShading);

        ShaderResourceViewDesc viewDesc = new()
        {
            ViewDimension = SrvDimension.RaytracingAccelerationStructure,
            Shader4ComponentMapping = DXGraphicsContext.Shader4ComponentMapping,
            RaytracingAccelerationStructure = new() { Location = AccelerationStructure.GPUVirtualAddress }
        };

        context.Device.CreateShaderResourceView(default(ID3D12Resource*), &viewDesc, (Token = context.CbvSrvUavHeap.Allocate()).CpuHandle);
    }

    public DXBuffer Instance { get; }

    public DXBuffer AccelerationStructure { get; }

    public DXBuffer Scratch { get; }

    public override ResourceHandle Handle => Token.ResourceHandle;

    public void Update(DXCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
        BuildRaytracingAccelerationStructureInputs inputs = Inputs(newDesc);
        inputs.Flags |= RaytracingAccelerationStructureBuildFlags.PerformUpdate;

        BuildRaytracingAccelerationStructureDesc buildDesc = new()
        {
            DestAccelerationStructureData = AccelerationStructure.GPUVirtualAddress,
            Inputs = inputs,
            SourceAccelerationStructureData = AccelerationStructure.GPUVirtualAddress,
            ScratchAccelerationStructureData = Scratch.GPUVirtualAddress
        };

        BuildSyncBarrier(commandBuffer, BarrierSync.BuildRaytracingAccelerationStructure);
        commandBuffer.CommandList.BuildRaytracingAccelerationStructure(&buildDesc, 0, default(RaytracingAccelerationStructurePostbuildInfoDesc*));
        BuildSyncBarrier(commandBuffer, BarrierSync.AllShading);
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return type switch
        {
            NativeObjectType.D3D12GpuVirtualAddress => (nint)AccelerationStructure.GPUVirtualAddress,
            NativeObjectType.D3D12Resource => (nint)AccelerationStructure.Resource.Handle,
            _ => default
        };
    }

    protected override void SetResourceName(string name)
    {
        AccelerationStructure.Name = name;
    }

    protected override void Destroy()
    {
        Token.Dispose();

        Scratch.Dispose();
        AccelerationStructure.Dispose();
        Instance.Dispose();
    }

    private BuildRaytracingAccelerationStructureInputs Inputs(TopLevelAccelerationStructureDesc desc)
    {
        uint instanceCount = (uint)desc.Instances.Length;

        nint pointer = Instance.Map();

        RaytracingInstanceDesc* instances = (RaytracingInstanceDesc*)pointer;
        for (uint i = 0; i < instanceCount; i++)
        {
            RayTracingInstance instance = desc.Instances[i];

            instances[i] = new()
            {
                InstanceID = instance.InstanceId,
                InstanceMask = instance.VisibilityMask,
                Flags = (uint)DXFormats.DirectX12(instance.Flags),
                AccelerationStructure = instance.AccelerationStructure.DirectX12().AccelerationStructure.GPUVirtualAddress
            };

            *(Matrix3X4<float>*)instances[i].Transform = DXFormats.DirectX12(instance.Transform);
        }

        Instance.Unmap();

        return new()
        {
            Type = RaytracingAccelerationStructureType.TopLevel,
            Flags = DXFormats.DirectX12(desc.BuildFlags),
            NumDescs = instanceCount,
            InstanceDescs = Instance.GPUVirtualAddress
        };
    }

    private static void BuildSyncBarrier(DXCommandBuffer commandBuffer, BarrierSync syncAfter)
    {
        GlobalBarrier barrier = new()
        {
            SyncBefore = BarrierSync.BuildRaytracingAccelerationStructure,
            SyncAfter = syncAfter,
            AccessBefore = BarrierAccess.RaytracingAccelerationStructureWrite,
            AccessAfter = BarrierAccess.RaytracingAccelerationStructureRead
        };

        BarrierGroup barrierGroup = new()
        {
            Type = BarrierType.Global,
            NumBarriers = 1,
            PGlobalBarriers = &barrier
        };

        commandBuffer.CommandList.Barrier(1, &barrierGroup);
    }
}
