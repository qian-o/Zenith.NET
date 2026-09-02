using Zenith.NET.Extensions.Upscaling.Passes;

namespace Zenith.NET.Extensions.Upscaling;

public class Upscaler : DisposableObject
{
    private readonly EasuPass easuPass;
    private readonly RcasPass rcasPass;
    private readonly Texture intermediate;

    private bool intermediateTransitioned;

    internal Upscaler(GraphicsContext context, UpscalerDesc desc)
    {
        easuPass = new(context);
        rcasPass = new(context);
        intermediate = context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = desc.Format,
            Width = desc.OutputWidth,
            Height = desc.OutputHeight,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Storage
        });

        Desc = desc;
    }

    public UpscalerDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, ResourceHandle input, ResourceHandle output)
    {
        commandBuffer.BeginDebugEvent("Upscaler");

        if (!intermediateTransitioned)
        {
            commandBuffer.Transition(intermediate, default, TextureLayout.Undefined, TextureLayout.Storage);

            intermediateTransitioned = true;
        }

        easuPass.Record(commandBuffer, Desc, input, intermediate.StorageHandle);
        rcasPass.Record(commandBuffer, Desc, intermediate.StorageHandle, output);

        commandBuffer.EndDebugEvent();
    }

    protected override void Destroy()
    {
        intermediate.Dispose();
        rcasPass.Dispose();
        easuPass.Dispose();
    }
}