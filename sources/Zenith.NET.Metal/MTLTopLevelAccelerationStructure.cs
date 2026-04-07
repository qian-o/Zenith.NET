using Metal.NET;

namespace Zenith.NET.Metal;

internal unsafe class MTLTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public MTLAccelerationStructure AccelerationStructure;

    public MTLTopLevelAccelerationStructure(MTLGraphicsContext context, TopLevelAccelerationStructureDesc desc, MTLCommandBuffer commandBuffer) : base(context, desc)
    {
        uint instanceCount = (uint)desc.Instances.Length;

        InstanceBuffer = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(MTLIndirectAccelerationStructureInstanceDescriptor) * instanceCount),
            StrideInBytes = (uint)sizeof(MTLIndirectAccelerationStructureInstanceDescriptor),
            Flags = BufferUsageFlags.MapWrite
        });

        FillInstanceBuffer(desc);

        MTL4InstanceAccelerationStructureDescriptor descriptor = new()
        {
            InstanceDescriptorBuffer = new(InstanceBuffer.Metal().GpuAddress, InstanceBuffer.Desc.SizeInBytes),
            InstanceDescriptorStride = (uint)sizeof(MTLIndirectAccelerationStructureInstanceDescriptor),
            InstanceCount = instanceCount,
            InstanceDescriptorType = MTLAccelerationStructureInstanceDescriptorType.Indirect,
            InstanceTransformationMatrixLayout = MTLMatrixLayout.RowMajor,
            Usage = MTLFormats.Metal(desc.Flags)
        };

        MTLAccelerationStructureSizes sizes = context.Device.AccelerationStructureSizes(descriptor);

        AccelerationStructure = context.Device.MakeAccelerationStructure(sizes.AccelerationStructureSize);
        context.AddAllocation(AccelerationStructure);

        ScratchBuffer = new(context, new()
        {
            SizeInBytes = (uint)sizes.BuildScratchBufferSize,
            StrideInBytes = (uint)sizes.BuildScratchBufferSize,
            Flags = BufferUsageFlags.ShaderResource
        });

        commandBuffer.CommandEncoder.Compute?.Build(AccelerationStructure, descriptor, new(ScratchBuffer.Buffer.GpuAddress, ScratchBuffer.Desc.SizeInBytes));
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public MTLBuffer InstanceBuffer { get; }

    public MTLBuffer ScratchBuffer { get; }

    public void Update(MTLCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
        FillInstanceBuffer(newDesc);

        MTL4InstanceAccelerationStructureDescriptor descriptor = new()
        {
            InstanceDescriptorBuffer = new(InstanceBuffer.Metal().GpuAddress, InstanceBuffer.Desc.SizeInBytes),
            InstanceDescriptorStride = (uint)sizeof(MTLIndirectAccelerationStructureInstanceDescriptor),
            InstanceCount = (uint)newDesc.Instances.Length,
            InstanceDescriptorType = MTLAccelerationStructureInstanceDescriptorType.Indirect,
            InstanceTransformationMatrixLayout = MTLMatrixLayout.RowMajor,
            Usage = MTLFormats.Metal(newDesc.Flags)
        };

        commandBuffer.CommandEncoder.Compute?.Refit(AccelerationStructure, descriptor, AccelerationStructure, new(ScratchBuffer.Buffer.GpuAddress, ScratchBuffer.Desc.SizeInBytes));
    }

    protected override void SetResourceName(string name)
    {
        AccelerationStructure.Label = name;
    }

    protected override void Destroy()
    {
        Context.RemoveAllocation(AccelerationStructure);

        AccelerationStructure.Dispose();

        ScratchBuffer.Dispose();
        InstanceBuffer.Dispose();
    }

    private void FillInstanceBuffer(TopLevelAccelerationStructureDesc desc)
    {
        uint instanceCount = (uint)desc.Instances.Length;

        MappedMemory mappedMemory = InstanceBuffer.Map();

        MTLIndirectAccelerationStructureInstanceDescriptor* instances = (MTLIndirectAccelerationStructureInstanceDescriptor*)mappedMemory.Pointer;
        for (uint i = 0; i < instanceCount; i++)
        {
            RayTracingInstance instance = desc.Instances[i];

            instances[i] = new()
            {
                TransformationMatrix = MTLFormats.Metal(instance.Transform),
                Options = MTLFormats.Metal(instance.Flags),
                Mask = instance.Mask,
                UserID = instance.ID,
                AccelerationStructureID = instance.AccelerationStructure.Metal().AccelerationStructure.GpuResourceID
            };
        }

        InstanceBuffer.Unmap();
    }
}
