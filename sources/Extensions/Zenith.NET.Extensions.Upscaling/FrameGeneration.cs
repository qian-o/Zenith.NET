namespace Zenith.NET.Extensions.Upscaling;

public abstract class FrameGeneration : GraphicsResource
{
    internal FrameGeneration(GraphicsContext context, FrameGenerationDesc desc) : base(context)
    {
        Desc = desc;
        Output = UpscalingBridge.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);
    }

    public FrameGenerationDesc Desc { get; }

    public Texture Output { get; }

    public void Dispatch(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        commandBuffer.BeginDebugEvent("Frame Generation");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
        Output.Name = name;
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args);

    protected override void Destroy()
    {
        Output.Dispose();
    }
}
