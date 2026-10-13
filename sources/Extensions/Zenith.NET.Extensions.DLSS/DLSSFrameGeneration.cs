namespace Zenith.NET.Extensions.DLSS;

public class DLSSFrameGeneration : DLSSFeature
{
    internal DLSSFrameGeneration(DLSSBackend backend, DLSSFrameGenerationDesc desc) : base(backend)
    {
        Desc = desc;

        using Lock.Scope _ = DLSSBackend.Lock.EnterScope();

        CommandBuffer commandBuffer = backend.Context.GraphicsQueue.CommandBuffer();
        backend.Create(commandBuffer, this);
        commandBuffer.Submit().Wait();
    }

    public DLSSFrameGenerationDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, DLSSFrameGenerationArgs args)
    {
        using Lock.Scope _ = DLSSBackend.Lock.EnterScope();

        commandBuffer.BeginDebugEvent("DLSS Frame Generation");
        commandBuffer.Barrier(BarrierStages.All, BarrierStages.All);

        Backend.Evaluate(commandBuffer, this, args);

        commandBuffer.Barrier(BarrierStages.All, BarrierStages.All);
        commandBuffer.EndDebugEvent();
    }
}
