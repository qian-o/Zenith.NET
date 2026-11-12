namespace Zenith.NET.Extensions.Upscaling;

public abstract class SuperResolution(GraphicsContext context, SuperResolutionDesc desc) : DisposableObject
{
    public SuperResolutionDesc Desc { get; } = desc;

    public Texture Output { get; } = Vendor.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);

    public void Dispatch(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        commandBuffer.BeginDebugEvent("Super Resolution");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args);

    protected override void Destroy()
    {
        Output.Dispose();
    }
}
