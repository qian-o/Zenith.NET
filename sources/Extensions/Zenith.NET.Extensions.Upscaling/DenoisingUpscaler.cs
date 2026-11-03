namespace Zenith.NET.Extensions.Upscaling;

public abstract class DenoisingUpscaler(DenoisingUpscalerDesc desc) : DisposableObject
{
    private readonly DenoisingUpscalerDesc desc = desc;

    public ref readonly DenoisingUpscalerDesc Desc => ref desc;

    public void Dispatch(CommandBuffer commandBuffer, DenoisingUpscalerArgs args)
    {
        commandBuffer.BeginExternalCommands();
        commandBuffer.BeginDebugEvent("Denoising Upscaler");

        try
        {
            DispatchImpl(commandBuffer, args);
        }
        finally
        {
            commandBuffer.EndDebugEvent();
            commandBuffer.EndExternalCommands();
        }
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, DenoisingUpscalerArgs args);
}
