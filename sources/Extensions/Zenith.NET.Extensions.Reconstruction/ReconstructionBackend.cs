using Zenith.NET.Extensions.Reconstruction.DLSS;
using Zenith.NET.Extensions.Reconstruction.MetalFX;

namespace Zenith.NET.Extensions.Reconstruction;

internal abstract class ReconstructionBackend(GraphicsContext context) : DisposableObject
{
    public GraphicsContext Context { get; } = context;

    public abstract ReconstructionCapabilities Capabilities { get; }

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

    protected abstract UpscalerInputSize GetTemporalUpscalerInputSizeImpl(uint outputWidth, uint outputHeight, UpscalerMode mode);

    protected abstract UpscalerInputSize GetDenoisingUpscalerInputSizeImpl(uint outputWidth, uint outputHeight, UpscalerMode mode);

    protected abstract TemporalUpscaler CreateTemporalUpscalerImpl(TemporalUpscalerDesc desc);

    protected abstract DenoisingUpscaler CreateDenoisingUpscalerImpl(DenoisingUpscalerDesc desc);

    protected abstract FrameGenerator CreateFrameGeneratorImpl(FrameGeneratorDesc desc);

    public static ReconstructionBackend Create(GraphicsContext context, string dataPath)
    {
        return context.GraphicsApi is GraphicsApi.Metal ? new MetalFXContext(context) : new DLSSContext(context, dataPath);
    }
}
