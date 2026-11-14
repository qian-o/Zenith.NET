namespace Zenith.NET.Extensions.Upscaling;

public abstract class FrameGeneration : DisposableObject
{
    internal FrameGeneration(GraphicsContext context, FrameGenerationDesc desc)
    {
        Desc = desc;
        Output = Vendor.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);
    }

    public FrameGenerationDesc Desc { get; }

    public Texture Output { get; }

    public void Dispatch(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        commandBuffer.BeginDebugEvent("Frame Generation");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args);

    protected override void Destroy()
    {
        Output.Dispose();
    }
}
