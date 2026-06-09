using Silk.NET.Direct3D12;
using Silk.NET.Maths;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public DXDescriptorToken Token;

    public DXTopLevelAccelerationStructure(DXGraphicsContext context, DXCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc desc) : base(context, desc)
    {
        InstanceBuffer = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(RaytracingInstanceDesc) * desc.Instances.Length),
            Residency = MemoryResidency.CpuWriteOnly
        });

        FillInputs(desc, out BuildRaytracingAccelerationStructureInputs inputs);

        RaytracingAccelerationStructurePrebuildInfo prebuildInfo = new();
        context.Device.GetRaytracingAccelerationStructurePrebuildInfo(&inputs, &prebuildInfo);

        AccelerationStructureBuffer = new(context, new()
        {
            SizeInBytes = (uint)prebuildInfo.ResultDataMaxSizeInBytes,
            Residency = MemoryResidency.GpuOnly
        }, ResourceFlags.RaytracingAccelerationStructure);

        ScratchBuffer = new(context, new()
        {
            SizeInBytes = (uint)prebuildInfo.ScratchDataSizeInBytes,
            Usages = BufferUsages.StorageReadWrite,
            Residency = MemoryResidency.GpuOnly
        });

        BuildRaytracingAccelerationStructureDesc buildDesc = new()
        {
            DestAccelerationStructureData = AccelerationStructureBuffer.GPUVirtualAddress,
            Inputs = inputs,
            ScratchAccelerationStructureData = ScratchBuffer.GPUVirtualAddress
        };

        commandBuffer.CommandList.BuildRaytracingAccelerationStructure(&buildDesc, 0, default(RaytracingAccelerationStructurePostbuildInfoDesc*));

        GlobalBarrier barrier = new()
        {
            SyncBefore = BarrierSync.BuildRaytracingAccelerationStructure,
            SyncAfter = BarrierSync.AllShading,
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

        ShaderResourceViewDesc viewDesc = new()
        {
            ViewDimension = SrvDimension.RaytracingAccelerationStructure,
            Shader4ComponentMapping = DXGraphicsContext.Shader4ComponentMapping,
            RaytracingAccelerationStructure = new()
            {
                Location = AccelerationStructureBuffer.GPUVirtualAddress
            }
        };

        context.Device.CreateShaderResourceView(default(ID3D12Resource*), &viewDesc, (Token = context.CbvSrvUavHeap.Allocate()).CpuHandle);
    }

    public DXBuffer InstanceBuffer { get; }

    public DXBuffer AccelerationStructureBuffer { get; }

    public DXBuffer ScratchBuffer { get; }

    public override ResourceHandle Handle => Token.ResourceHandle;

    public void Update(DXCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
        FillInputs(newDesc, out BuildRaytracingAccelerationStructureInputs inputs);
        inputs.Flags |= RaytracingAccelerationStructureBuildFlags.PerformUpdate;

        BuildRaytracingAccelerationStructureDesc buildDesc = new()
        {
            DestAccelerationStructureData = AccelerationStructureBuffer.GPUVirtualAddress,
            Inputs = inputs,
            SourceAccelerationStructureData = AccelerationStructureBuffer.GPUVirtualAddress,
            ScratchAccelerationStructureData = ScratchBuffer.GPUVirtualAddress
        };

        commandBuffer.CommandList.BuildRaytracingAccelerationStructure(&buildDesc, 0, default(RaytracingAccelerationStructurePostbuildInfoDesc*));

        GlobalBarrier barrier = new()
        {
            SyncBefore = BarrierSync.BuildRaytracingAccelerationStructure,
            SyncAfter = BarrierSync.AllShading,
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

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
        AccelerationStructureBuffer.Name = name;
    }

    protected override void Destroy()
    {
        Token.Dispose();

        ScratchBuffer.Dispose();
        AccelerationStructureBuffer.Dispose();
        InstanceBuffer.Dispose();
    }

    private void FillInputs(TopLevelAccelerationStructureDesc desc, out BuildRaytracingAccelerationStructureInputs inputs)
    {
        uint instanceCount = (uint)desc.Instances.Length;

        MappedMemory mappedMemory = InstanceBuffer.Map();

        RaytracingInstanceDesc* instances = (RaytracingInstanceDesc*)mappedMemory.Pointer;
        for (uint i = 0; i < instanceCount; i++)
        {
            RayTracingInstance instance = desc.Instances[i];

            instances[i] = new()
            {
                InstanceID = instance.InstanceId,
                InstanceMask = instance.VisibilityMask,
                Flags = (uint)DXFormats.DirectX12(instance.Flags),
                AccelerationStructure = instance.AccelerationStructure.DirectX12().AccelerationStructureBuffer.GPUVirtualAddress
            };

            *(Matrix3X4<float>*)instances[i].Transform = DXFormats.DirectX12(instance.Transform);
        }

        InstanceBuffer.Unmap();

        inputs = new()
        {
            Type = RaytracingAccelerationStructureType.TopLevel,
            Flags = DXFormats.DirectX12(desc.BuildFlags),
            NumDescs = instanceCount,
            InstanceDescs = InstanceBuffer.GPUVirtualAddress
        };
    }
}
