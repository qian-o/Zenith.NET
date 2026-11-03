namespace Zenith.NET.Extensions.Upscaling;

internal abstract class UpscalingProvider : DisposableObject
{
    protected UpscalingProvider(GraphicsContext context)
    {
        Context = context;

        Initialize();
    }

    public GraphicsContext Context { get; }

    public abstract bool TemporalUpscalerSupported { get; }

    public abstract bool DenoisingUpscalerSupported { get; }

    public abstract bool FrameGeneratorSupported { get; }

    public abstract UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

    public abstract UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

    public abstract TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc);

    public abstract DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc);

    public abstract FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc);

    protected abstract void Initialize();
}
