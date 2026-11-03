namespace Zenith.NET.Extensions.Upscaling;

internal abstract class Provider : DisposableObject
{
    protected Provider(GraphicsContext context)
    {
        Context = context;

        Initialize(out UpscalingCapabilities capabilities);

        Capabilities = capabilities;
    }

    public GraphicsContext Context { get; }

    public UpscalingCapabilities Capabilities { get; }

    public UpscalerInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalerMode mode)
    {
        return GetTemporalUpscalerInputSizeImpl(outputWidth, outputHeight, mode);
    }

    public UpscalerInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalerMode mode)
    {
        return GetDenoisingUpscalerInputSizeImpl(outputWidth, outputHeight, mode);
    }

    public TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc)
    {
        return CreateTemporalUpscalerImpl(desc);
    }

    public DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc)
    {
        return CreateDenoisingUpscalerImpl(desc);
    }

    public FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc)
    {
        return CreateFrameGeneratorImpl(desc);
    }

    protected abstract void Initialize(out UpscalingCapabilities capabilities);

    protected abstract UpscalerInputSize GetTemporalUpscalerInputSizeImpl(uint outputWidth, uint outputHeight, UpscalerMode mode);

    protected abstract UpscalerInputSize GetDenoisingUpscalerInputSizeImpl(uint outputWidth, uint outputHeight, UpscalerMode mode);

    protected abstract TemporalUpscaler CreateTemporalUpscalerImpl(TemporalUpscalerDesc desc);

    protected abstract DenoisingUpscaler CreateDenoisingUpscalerImpl(DenoisingUpscalerDesc desc);

    protected abstract FrameGenerator CreateFrameGeneratorImpl(FrameGeneratorDesc desc);
}
