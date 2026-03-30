using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public DXDescriptorToken Token;

    public DXTopLevelAccelerationStructure(DXGraphicsContext context, TopLevelAccelerationStructureDesc desc, DXCommandBuffer commandBuffer) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        InstanceBuffer = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(RaytracingInstanceDesc) * desc.Instances.Length),
            StrideInBytes = (uint)sizeof(RaytracingInstanceDesc),
            Flags = BufferUsageFlags.MapWrite
        });

        FillInstanceBuffer(desc, out BuildRaytracingAccelerationStructureInputs inputs);

        RaytracingAccelerationStructurePrebuildInfo prebuildInfo = new();

        context.Device5?.GetRaytracingAccelerationStructurePrebuildInfo(&inputs, &prebuildInfo);

        AccelerationStructureBuffer = new(context, new()
        {
            SizeInBytes = (uint)prebuildInfo.ResultDataMaxSizeInBytes,
            StrideInBytes = (uint)prebuildInfo.ResultDataMaxSizeInBytes,
            Flags = BufferUsageFlags.AccelerationStructure
        });

        ScratchBuffer = new(context, new()
        {
            SizeInBytes = (uint)prebuildInfo.ScratchDataSizeInBytes,
            StrideInBytes = (uint)prebuildInfo.ScratchDataSizeInBytes,
            Flags = BufferUsageFlags.UnorderedAccess
        });

        BuildRaytracingAccelerationStructureDesc buildDesc = new()
        {
            DestAccelerationStructureData = AccelerationStructureBuffer.GPUVirtualAddress,
            Inputs = inputs,
            ScratchAccelerationStructureData = ScratchBuffer.GPUVirtualAddress
        };

        commandBuffer.GraphicsCommandList4.BuildRaytracingAccelerationStructure(&buildDesc, 0, (RaytracingAccelerationStructurePostbuildInfoDesc*)null);

        ResourceBarrier barrier = new()
        {
            Type = ResourceBarrierType.Uav,
            UAV = new()
            {
                PResource = AccelerationStructureBuffer.Resource
            }
        };

        commandBuffer.GraphicsCommandList4.ResourceBarrier(1, &barrier);

        ShaderResourceViewDesc viewDesc = new()
        {
            ViewDimension = SrvDimension.RaytracingAccelerationStructure,
            Shader4ComponentMapping = DXGraphicsContext.Shader4ComponentMapping,
            RaytracingAccelerationStructure = new()
            {
                Location = AccelerationStructureBuffer.GPUVirtualAddress
            }
        };

        context.Device.CreateShaderResourceView((ID3D12Resource*)null, &viewDesc, (Token = context.CbvSrvUavAllocator.Allocate(1)).Handle);
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXBuffer InstanceBuffer { get; }

    public DXBuffer AccelerationStructureBuffer { get; }

    public DXBuffer ScratchBuffer { get; }

    public void Update(DXCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
        FillInstanceBuffer(newDesc, out BuildRaytracingAccelerationStructureInputs inputs);

        BuildRaytracingAccelerationStructureDesc buildDesc = new()
        {
            DestAccelerationStructureData = AccelerationStructureBuffer.GPUVirtualAddress,
            Inputs = inputs,
            SourceAccelerationStructureData = AccelerationStructureBuffer.GPUVirtualAddress,
            ScratchAccelerationStructureData = ScratchBuffer.GPUVirtualAddress
        };

        commandBuffer.GraphicsCommandList4.BuildRaytracingAccelerationStructure(&buildDesc, 0, (RaytracingAccelerationStructurePostbuildInfoDesc*)null);

        ResourceBarrier barrier = new()
        {
            Type = ResourceBarrierType.Uav,
            UAV = new()
            {
                PResource = AccelerationStructureBuffer.Resource
            }
        };

        commandBuffer.GraphicsCommandList4.ResourceBarrier(1, &barrier);
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Token.Dispose();

        ScratchBuffer.Dispose();
        AccelerationStructureBuffer.Dispose();
        InstanceBuffer.Dispose();
    }

    private void FillInstanceBuffer(TopLevelAccelerationStructureDesc desc, out BuildRaytracingAccelerationStructureInputs inputs)
    {
        uint instanceCount = (uint)desc.Instances.Length;

        MappedMemory mappedMemory = InstanceBuffer.Map();

        RaytracingInstanceDesc* instances = (RaytracingInstanceDesc*)mappedMemory.Pointer;
        for (uint i = 0; i < instanceCount; i++)
        {
            RayTracingInstance instance = desc.Instances[i];

            instances[i] = new()
            {
                InstanceID = instance.ID,
                InstanceMask = instance.Mask,
                Flags = (uint)DXFormats.DirectX12(instance.Flags),
                AccelerationStructure = instance.AccelerationStructure.DirectX12().AccelerationStructureBuffer.GPUVirtualAddress
            };

            new ReadOnlySpan<float>(&instance.Transform, 12).CopyTo(new(instances[i].Transform, 12));
        }

        InstanceBuffer.Unmap();

        inputs = new()
        {
            Type = RaytracingAccelerationStructureType.TopLevel,
            Flags = DXFormats.DirectX12(desc.Flags),
            NumDescs = instanceCount,
            InstanceDescs = InstanceBuffer.GPUVirtualAddress
        };
    }
}
