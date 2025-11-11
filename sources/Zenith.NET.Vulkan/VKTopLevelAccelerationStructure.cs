namespace Zenith.NET;

internal unsafe class VKTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public VKTopLevelAccelerationStructure(VKGraphicsContext context, TopLevelAccelerationStructureDesc desc, VKCommandBuffer commandBuffer) : base(context, desc)
    {
    }

    public void Update(VKCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
        throw new NotImplementedException();
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
