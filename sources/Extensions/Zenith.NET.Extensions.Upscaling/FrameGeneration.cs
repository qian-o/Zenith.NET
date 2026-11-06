namespace Zenith.NET.Extensions.Upscaling;

public abstract class FrameGeneration(FrameGenerationDesc desc) : DisposableObject
{
    private readonly FrameGenerationDesc desc = desc;

    public ref readonly FrameGenerationDesc Desc => ref desc;

    public void Dispatch(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        commandBuffer.BeginExternalCommands();
        commandBuffer.BeginDebugEvent("Frame Generation");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
        commandBuffer.EndExternalCommands();
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args);
}
