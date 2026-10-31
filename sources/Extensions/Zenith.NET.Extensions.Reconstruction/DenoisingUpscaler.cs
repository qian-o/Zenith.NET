namespace Zenith.NET.Extensions.Reconstruction;

public abstract class DenoisingUpscaler : DisposableObject
{
    private readonly DenoisingUpscalerDesc desc;

    internal DenoisingUpscaler(DenoisingUpscalerDesc desc)
    {
        this.desc = desc;
    }

    public ref readonly DenoisingUpscalerDesc Desc => ref desc;

    public void Dispatch(CommandBuffer commandBuffer, DenoisingUpscalerArgs args)
    {
        commandBuffer.BeginExternalCommands();
        commandBuffer.BeginDebugEvent("Denoising Upscaler");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
        commandBuffer.EndExternalCommands();
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, DenoisingUpscalerArgs args);
}
