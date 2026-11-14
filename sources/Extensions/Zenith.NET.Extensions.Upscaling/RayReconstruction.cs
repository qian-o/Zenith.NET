namespace Zenith.NET.Extensions.Upscaling;

public abstract class RayReconstruction : DisposableObject
{
    internal RayReconstruction(GraphicsContext context, RayReconstructionDesc desc)
    {
        Desc = desc;
        Output = Vendor.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);
    }

    public RayReconstructionDesc Desc { get; }

    public Texture Output { get; }

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
