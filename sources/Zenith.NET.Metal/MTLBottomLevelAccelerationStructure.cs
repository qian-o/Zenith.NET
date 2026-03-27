using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public MTLAccelerationStructure AccelerationStructure;

    public MTLBottomLevelAccelerationStructure(GraphicsContext context, BottomLevelAccelerationStructureDesc desc, MTLCommandBuffer commandBuffer) : base(context, desc)
    {
        throw new NotImplementedException();
    }

    public MTLBuffer TransformBuffer { get; }

    public MTLBuffer ScratchBuffer { get; }

    protected override void SetResourceName(string name)
    {
        AccelerationStructure.Label = name;
    }

    protected override void Destroy()
    {
        AccelerationStructure.Dispose();

        ScratchBuffer.Dispose();
        TransformBuffer.Dispose();
    }
}
