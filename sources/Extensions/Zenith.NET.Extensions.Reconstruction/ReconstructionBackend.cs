namespace Zenith.NET.Extensions.Reconstruction;

internal abstract class ReconstructionBackend : DisposableObject
{
    protected ReconstructionBackend(GraphicsContext context, string dataPath)
    {
        Context = context;

        Initialize(dataPath, out ReconstructionCapabilities capabilities);

        Capabilities = capabilities;
    }

    public GraphicsContext Context { get; }

    public ReconstructionCapabilities Capabilities { get; }

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

    protected abstract void Initialize(string dataPath, out ReconstructionCapabilities capabilities);

    protected abstract UpscalerInputSize GetTemporalUpscalerInputSizeImpl(uint outputWidth, uint outputHeight, UpscalerMode mode);

    protected abstract UpscalerInputSize GetDenoisingUpscalerInputSizeImpl(uint outputWidth, uint outputHeight, UpscalerMode mode);

    protected abstract TemporalUpscaler CreateTemporalUpscalerImpl(TemporalUpscalerDesc desc);

    protected abstract DenoisingUpscaler CreateDenoisingUpscalerImpl(DenoisingUpscalerDesc desc);

    protected abstract FrameGenerator CreateFrameGeneratorImpl(FrameGeneratorDesc desc);
}
