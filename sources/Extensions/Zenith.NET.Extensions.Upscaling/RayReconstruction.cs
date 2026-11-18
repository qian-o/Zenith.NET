namespace Zenith.NET.Extensions.Upscaling;

public abstract class RayReconstruction : GraphicsResource
{
    internal RayReconstruction(GraphicsContext context, RayReconstructionDesc desc) : base(context)
    {
        Desc = desc;
        Output = UpscalingBridge.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);
    }

    public RayReconstructionDesc Desc { get; }

    public Texture Output { get; }

    public void Dispatch(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        commandBuffer.BeginDebugEvent("Ray Reconstruction");

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

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args);

    protected override void Destroy()
    {
        Output.Dispose();
    }
}
