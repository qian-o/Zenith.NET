namespace Zenith.NET.Extensions.DLSS;

public class DLSSSuperResolution : DLSSFeature
{
    internal DLSSSuperResolution(DLSSBackend backend, DLSSSuperResolutionDesc desc) : base(backend)
    {
        Desc = desc;

        CommandBuffer commandBuffer = backend.Context.GraphicsQueue.CommandBuffer();
        backend.Create(commandBuffer, this);
        commandBuffer.Submit().Wait();
    }

    public DLSSSuperResolutionDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, DLSSSuperResolutionArgs args)
    {
        commandBuffer.BeginDebugEvent("DLSS Super Resolution");
        commandBuffer.BeginExternalCommands();

        Backend.Evaluate(commandBuffer, this, args);

        commandBuffer.EndExternalCommands();
        commandBuffer.EndDebugEvent();
    }
}
