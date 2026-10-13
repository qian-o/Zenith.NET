namespace Zenith.NET.Extensions.DLSS;

public class DLSSRayReconstruction : DLSSFeature
{
    internal DLSSRayReconstruction(DLSSBackend backend, DLSSRayReconstructionDesc desc) : base(backend)
    {
        Desc = desc;

        using Lock.Scope _ = DLSSBackend.Lock.EnterScope();

        CommandBuffer commandBuffer = backend.Context.GraphicsQueue.CommandBuffer();
        backend.Create(commandBuffer, this);
        commandBuffer.Submit().Wait();
    }

    public DLSSRayReconstructionDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, DLSSRayReconstructionArgs args)
    {
        using Lock.Scope _ = DLSSBackend.Lock.EnterScope();

        commandBuffer.BeginDebugEvent("DLSS Ray Reconstruction");
        commandBuffer.Barrier(BarrierStages.All, BarrierStages.All);

        Backend.Evaluate(commandBuffer, this, args);

        commandBuffer.Barrier(BarrierStages.All, BarrierStages.All);
        commandBuffer.EndDebugEvent();
    }
}
