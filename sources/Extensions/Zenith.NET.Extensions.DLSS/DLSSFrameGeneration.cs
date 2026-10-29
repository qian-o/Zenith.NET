namespace Zenith.NET.Extensions.DLSS;

public class DLSSFrameGeneration : DLSSFeature
{
    internal DLSSFrameGeneration(DLSSBackend backend, DLSSFrameGenerationDesc desc) : base(backend)
    {
        Desc = desc;

        CommandBuffer commandBuffer = backend.Context.GraphicsQueue.CommandBuffer();
        backend.Create(commandBuffer, this);
        commandBuffer.Submit().Wait();
    }

    public DLSSFrameGenerationDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, DLSSFrameGenerationArgs args)
    {
        commandBuffer.BeginDebugEvent("DLSS Frame Generation");
        commandBuffer.BeginExternalCommands();

        Backend.Evaluate(commandBuffer, this, args);

        commandBuffer.EndExternalCommands();
        commandBuffer.EndDebugEvent();
    }
}
