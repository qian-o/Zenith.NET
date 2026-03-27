using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public MTLAccelerationStructure AccelerationStructure;

    public MTLTopLevelAccelerationStructure(GraphicsContext context, TopLevelAccelerationStructureDesc desc, MTLCommandBuffer commandBuffer) : base(context, desc)
    {
        throw new NotImplementedException();
    }

    public MTLBuffer InstanceBuffer { get; }

    public MTLBuffer ScratchBuffer { get; }

    public void Update(MTLCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
        AccelerationStructure.Label = name;
    }

    protected override void Destroy()
    {
        AccelerationStructure.Dispose();

        ScratchBuffer.Dispose();
        InstanceBuffer.Dispose();
    }
}
