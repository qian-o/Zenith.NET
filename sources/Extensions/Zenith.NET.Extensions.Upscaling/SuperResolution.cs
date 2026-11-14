namespace Zenith.NET.Extensions.Upscaling;

public abstract class SuperResolution : DisposableObject
{
    internal SuperResolution(GraphicsContext context, SuperResolutionDesc desc)
    {
        Desc = desc;
        Output = Vendor.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);
    }

    public SuperResolutionDesc Desc { get; }

    public Texture Output { get; }

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
