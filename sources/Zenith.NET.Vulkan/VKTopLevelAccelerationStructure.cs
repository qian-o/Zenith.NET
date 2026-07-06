namespace Zenith.NET.Vulkan;

internal class VKTopLevelAccelerationStructure : TopLevelAccelerationStructure
{
    public VKTopLevelAccelerationStructure(VKGraphicsContext context, VKCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc desc) : base(context, desc)
    {
    }

    public override ResourceHandle Handle { get; }

    public void Update(VKCommandBuffer commandBuffer, TopLevelAccelerationStructureDesc newDesc)
    {
    }

    public override nint GetNativeObject(NativeObjectType type)
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
