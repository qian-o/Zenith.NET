namespace Zenith.NET.Extensions.Upscaling;

public abstract class FrameGeneration(GraphicsContext context, FrameGenerationDesc desc) : DisposableObject
{
    public FrameGenerationDesc Desc { get; } = desc;

    public Texture Output { get; } = Vendor.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);

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
