namespace Zenith.NET;

internal unsafe class VKBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public VKBottomLevelAccelerationStructure(VKGraphicsContext context, BottomLevelAccelerationStructureDesc desc, VKCommandBuffer commandBuffer) : base(context, desc)
    {
    }

    protected override void SetResourceName(string name)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        throw new NotImplementedException();
    }
}
