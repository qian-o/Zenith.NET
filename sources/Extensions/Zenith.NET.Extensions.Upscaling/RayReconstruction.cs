namespace Zenith.NET.Extensions.Upscaling;

public abstract class RayReconstruction(RayReconstructionDesc desc) : DisposableObject
{
    public RayReconstructionDesc Desc { get; } = desc;

    public void Dispatch(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        commandBuffer.BeginExternalCommands();
        commandBuffer.BeginDebugEvent("Ray Reconstruction");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
        commandBuffer.EndExternalCommands();
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args);
}
