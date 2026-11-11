namespace Zenith.NET.Extensions.Upscaling;

public abstract class FrameGeneration(FrameGenerationDesc desc) : DisposableObject
{
    public FrameGenerationDesc Desc { get; } = desc;

    public void Dispatch(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        commandBuffer.BeginDebugEvent("Frame Generation");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args);
}
