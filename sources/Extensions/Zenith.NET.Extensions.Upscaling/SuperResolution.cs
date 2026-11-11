namespace Zenith.NET.Extensions.Upscaling;

public abstract class SuperResolution(SuperResolutionDesc desc) : DisposableObject
{
    public SuperResolutionDesc Desc { get; } = desc;

    public void Dispatch(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        commandBuffer.BeginDebugEvent("Super Resolution");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args);
}
