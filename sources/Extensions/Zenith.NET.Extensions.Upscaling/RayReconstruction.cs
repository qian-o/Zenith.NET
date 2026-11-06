namespace Zenith.NET.Extensions.Upscaling;

public abstract class RayReconstruction(RayReconstructionDesc desc) : DisposableObject
{
    private readonly RayReconstructionDesc desc = desc;

    public ref readonly RayReconstructionDesc Desc => ref desc;

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
