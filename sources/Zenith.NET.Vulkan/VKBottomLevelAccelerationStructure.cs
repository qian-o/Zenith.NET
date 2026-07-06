namespace Zenith.NET.Vulkan;

internal class VKBottomLevelAccelerationStructure : BottomLevelAccelerationStructure
{
    public VKBottomLevelAccelerationStructure(VKGraphicsContext context, VKCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc desc) : base(context, desc)
    {
    }

    public void Update(VKCommandBuffer commandBuffer, BottomLevelAccelerationStructureDesc newDesc)
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
