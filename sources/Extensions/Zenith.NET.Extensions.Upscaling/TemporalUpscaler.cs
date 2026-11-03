namespace Zenith.NET.Extensions.Upscaling;

public abstract class TemporalUpscaler(TemporalUpscalerDesc desc) : DisposableObject
{
    private readonly TemporalUpscalerDesc desc = desc;

    public ref readonly TemporalUpscalerDesc Desc => ref desc;

    public void Dispatch(CommandBuffer commandBuffer, TemporalUpscalerArgs args)
    {
        commandBuffer.BeginExternalCommands();
        commandBuffer.BeginDebugEvent("Temporal Upscaler");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
        commandBuffer.EndExternalCommands();
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, TemporalUpscalerArgs args);
}
