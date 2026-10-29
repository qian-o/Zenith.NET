namespace Zenith.NET.Extensions.DLSS;

public class DLSSRayReconstruction : DLSSFeature
{
    internal DLSSRayReconstruction(DLSSBackend backend, DLSSRayReconstructionDesc desc) : base(backend)
    {
        Desc = desc;

        CommandBuffer commandBuffer = backend.Context.GraphicsQueue.CommandBuffer();
        backend.Create(commandBuffer, this);
        commandBuffer.Submit().Wait();
    }

    public DLSSRayReconstructionDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, DLSSRayReconstructionArgs args)
    {
        commandBuffer.BeginDebugEvent("DLSS Ray Reconstruction");
        commandBuffer.BeginExternalCommands();

        Backend.Evaluate(commandBuffer, this, args);

        commandBuffer.EndExternalCommands();
        commandBuffer.EndDebugEvent();
    }
}
