using Metal.NET;

namespace Zenith.NET.Metal;

internal unsafe class MTLTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public MTLAccelerationStructure AccelerationStructure;

    public MTLTopLevelAccelerationStructure(MTLGraphicsContext context, MTLCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc desc) : base(context, desc)
    {
        Instance = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(MTLIndirectAccelerationStructureInstanceDescriptor) * desc.Instances.Length),
            Residency = MemoryResidency.CpuWriteOnly
        });

        MTL4InstanceAccelerationStructureDescriptor descriptor = Descriptor(desc);

        MTLAccelerationStructureSizes sizes = context.Device.AccelerationStructureSizes(descriptor);

        context.Register(AccelerationStructure = context.Device.MakeAccelerationStructure(sizes.AccelerationStructureSize));

        Scratch = new(context, new()
        {
            SizeInBytes = (uint)sizes.BuildScratchBufferSize,
            Usages = BufferUsages.StorageReadWrite,
            Residency = MemoryResidency.GpuOnly
        });

        commandBuffer.Compute?.Build(AccelerationStructure, descriptor, new(Scratch.Buffer.GpuAddress, Scratch.Desc.SizeInBytes));
        commandBuffer.Compute?.BarrierAfterStages(MTLStages.AccelerationStructure, MTLStages.Dispatch, MTL4VisibilityOptions.Device);

        Handle = AccelerationStructure.GpuResourceID.Impl.ToHandle();
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public MTLBuffer Instance { get; }

    public MTLBuffer Scratch { get; }

    public override ResourceHandle Handle { get; }

    public void Update(MTLCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
        MTL4InstanceAccelerationStructureDescriptor descriptor = Descriptor(newDesc);
        descriptor.Usage |= MTLAccelerationStructureUsage.Refit;

        commandBuffer.Compute?.Refit(AccelerationStructure, descriptor, AccelerationStructure, new(Scratch.Buffer.GpuAddress, Scratch.Desc.SizeInBytes));
        commandBuffer.Compute?.BarrierAfterStages(MTLStages.AccelerationStructure, MTLStages.Dispatch, MTL4VisibilityOptions.Device);
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
        AccelerationStructure.Label = name;
    }

    protected override void Destroy()
    {
        Context.Unregister(AccelerationStructure);

        Scratch.Dispose();
        Instance.Dispose();
        AccelerationStructure.Dispose();
    }

    private MTL4InstanceAccelerationStructureDescriptor Descriptor(TopLevelAccelerationStructureDesc desc)
    {
        uint instanceCount = (uint)desc.Instances.Length;

        nint pointer = Instance.Map();

        MTLIndirectAccelerationStructureInstanceDescriptor* instances = (MTLIndirectAccelerationStructureInstanceDescriptor*)pointer;
        for (uint i = 0; i < instanceCount; i++)
        {
            RayTracingInstance instance = desc.Instances[i];

            instances[i] = new()
            {
                TransformationMatrix = MTLFormats.Metal(instance.Transform),
                Options = MTLFormats.Metal(instance.Flags),
                Mask = instance.VisibilityMask,
                UserID = instance.InstanceId,
                AccelerationStructureID = instance.AccelerationStructure.Metal().AccelerationStructure.GpuResourceID
            };
        }

        Instance.Unmap();

        return new()
        {
            InstanceDescriptorBuffer = new()
            {
                BufferAddress = Instance.Buffer.GpuAddress,
                Length = Instance.Desc.SizeInBytes
            },
            InstanceDescriptorStride = (uint)sizeof(MTLIndirectAccelerationStructureInstanceDescriptor),
            InstanceCount = instanceCount,
            InstanceDescriptorType = MTLAccelerationStructureInstanceDescriptorType.Indirect,
            InstanceTransformationMatrixLayout = MTLMatrixLayout.RowMajor,
            Usage = MTLFormats.Metal(desc.BuildFlags)
        };
    }
}
