namespace Zenith.NET.Extensions.Upscaling;

public abstract class SuperResolution(SuperResolutionDesc desc) : DisposableObject
{
    private readonly SuperResolutionDesc desc = desc;

    public ref readonly SuperResolutionDesc Desc => ref desc;

    public void Dispatch(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        commandBuffer.BeginExternalCommands();
        commandBuffer.BeginDebugEvent("Super Resolution");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
        commandBuffer.EndExternalCommands();
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args);
}
