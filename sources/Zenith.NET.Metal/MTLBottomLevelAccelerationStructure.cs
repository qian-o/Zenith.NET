using Metal.NET;

namespace Zenith.NET.Metal;

internal unsafe class MTLBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public MTLAccelerationStructure AccelerationStructure;

    public MTLBottomLevelAccelerationStructure(MTLGraphicsContext context, MTLCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc desc) : base(context, desc)
    {
        Transform = new(context, new()
        {
            SizeInBytes = (uint)(sizeof(MTLPackedFloat4x3) * desc.Geometries.Length),
            Residency = MemoryResidency.CpuWriteOnly
        });

        MTL4PrimitiveAccelerationStructureDescriptor descriptor = Descriptor(desc);

        MTLAccelerationStructureSizes sizes = context.Device.AccelerationStructureSizes(descriptor);

        context.Register(AccelerationStructure = context.Device.MakeAccelerationStructure(sizes.AccelerationStructureSize));

        Scratch = new(context, new()
        {
            SizeInBytes = (uint)sizes.BuildScratchBufferSize,
            Usages = BufferUsages.StorageReadWrite,
            Residency = MemoryResidency.GpuOnly
        });

        commandBuffer.Compute?.Build(AccelerationStructure, descriptor, new(Scratch.Buffer.GpuAddress, Scratch.Desc.SizeInBytes));
        commandBuffer.Compute?.BarrierAfterStages(MTLStages.AccelerationStructure, MTLStages.AccelerationStructure, MTL4VisibilityOptions.Device);
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public MTLBuffer Transform { get; }

    public MTLBuffer Scratch { get; }

    public void Update(MTLCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc newDesc)
    {
        MTL4PrimitiveAccelerationStructureDescriptor descriptor = Descriptor(newDesc);
        descriptor.Usage |= MTLAccelerationStructureUsage.Refit;

        commandBuffer.Compute?.Refit(AccelerationStructure, descriptor, AccelerationStructure, new(Scratch.Buffer.GpuAddress, Scratch.Desc.SizeInBytes));
        commandBuffer.Compute?.BarrierAfterStages(MTLStages.AccelerationStructure, MTLStages.AccelerationStructure, MTL4VisibilityOptions.Device);
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
        Transform.Dispose();
        AccelerationStructure.Dispose();
    }

    private MTL4PrimitiveAccelerationStructureDescriptor Descriptor(BottomLevelAccelerationStructureDesc desc)
    {
        throw new NotImplementedException();
    }
}
