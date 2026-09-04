using Zenith.NET.Extensions.Upscaling.Passes;

namespace Zenith.NET.Extensions.Upscaling;

public class SpatialUpscaler : DisposableObject
{
    private readonly Sgsr1Pass sgsr1Pass;

    internal SpatialUpscaler(GraphicsContext context, SpatialUpscalerDesc desc)
    {
        sgsr1Pass = new(context);

        Desc = desc;
    }

    public SpatialUpscalerDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, SpatialUpscalerArgs args)
    {
        commandBuffer.BeginDebugEvent("SpatialUpscaler");

        sgsr1Pass.Record(commandBuffer, Desc, args);

        commandBuffer.EndDebugEvent();
    }

    protected override void Destroy()
    {
        sgsr1Pass.Dispose();
    }
}
