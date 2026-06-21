namespace Zenith.NET.Metal;

internal class MTLBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public MTLBottomLevelAccelerationStructure(MTLGraphicsContext context, MTLCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc desc) : base(context, desc)
    {
    }

    public void Update(MTLCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc newDesc)
    {
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
