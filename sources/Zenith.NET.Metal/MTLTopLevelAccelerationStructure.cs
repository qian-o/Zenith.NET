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
        throw new NotImplementedException();
    }
}
