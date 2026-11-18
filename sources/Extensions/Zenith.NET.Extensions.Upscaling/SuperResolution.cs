namespace Zenith.NET.Extensions.Upscaling;

public abstract class SuperResolution : GraphicsResource
{
    internal SuperResolution(GraphicsContext context, SuperResolutionDesc desc) : base(context)
    {
        Desc = desc;
        Output = UpscalingBridge.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);
    }

    public SuperResolutionDesc Desc { get; }

    public Texture Output { get; }

    public void Dispatch(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        commandBuffer.BeginDebugEvent("Super Resolution");

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

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args);

    protected override void Destroy()
    {
        Output.Dispose();
    }
}
