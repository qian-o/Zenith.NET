namespace Zenith.NET.Metal;

internal class MTLTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public MTLTopLevelAccelerationStructure(MTLGraphicsContext context, MTLCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc desc) : base(context, desc)
    {
    }

    public override ResourceHandle Handle { get; }

    public void Update(MTLCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
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
