namespace Zenith.NET.Extensions.Upscaling;

public abstract class RayReconstruction(GraphicsContext context, RayReconstructionDesc desc) : DisposableObject
{
    public RayReconstructionDesc Desc { get; } = desc;

    public Texture Output { get; } = Vendor.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);

    public void Dispatch(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        commandBuffer.BeginDebugEvent("Ray Reconstruction");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args);

    protected override void Destroy()
    {
        Output.Dispose();
    }
}
