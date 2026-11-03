using Zenith.NET.Extensions.Upscaling.DLSS;
using Zenith.NET.Extensions.Upscaling.MetalFX;

namespace Zenith.NET.Extensions.Upscaling;

internal class UpscalingBackend : DisposableObject
{
    private readonly List<IUpscalingBackend> backends = [];
    private readonly IUpscalingBackend? temporalUpscalerBackend;
    private readonly IUpscalingBackend? denoisingUpscalerBackend;
    private readonly IUpscalingBackend? frameGeneratorBackend;

    public UpscalingBackend(GraphicsContext context, ReadOnlySpan<UpscalingApi> apis)
    {
        foreach (UpscalingApi api in apis.ToArray().Distinct())
        {
            if (temporalUpscalerBackend is not null && denoisingUpscalerBackend is not null && frameGeneratorBackend is not null)
            {
                break;
            }

            if (CreateBackend(context, api) is not IUpscalingBackend backend)
            {
                continue;
            }

            UpscalingCapabilities capabilities = backend.Capabilities;
            temporalUpscalerBackend ??= capabilities.TemporalUpscalerSupported ? backend : null;
            denoisingUpscalerBackend ??= capabilities.DenoisingUpscalerSupported ? backend : null;
            frameGeneratorBackend ??= capabilities.FrameGeneratorSupported ? backend : null;

            if (backend == temporalUpscalerBackend || backend == denoisingUpscalerBackend || backend == frameGeneratorBackend)
            {
                backends.Add(backend);
            }
            else
            {
                backend.Dispose();
            }
        }

        if (backends.Count is 0)
        {
            throw new NotSupportedException("This device does not support any of the specified upscaling APIs.");
        }

        Capabilities = new(temporalUpscalerBackend is not null, denoisingUpscalerBackend is not null, frameGeneratorBackend is not null);
    }

    public UpscalingCapabilities Capabilities { get; }

    public UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetBackend(temporalUpscalerBackend, "temporal upscaler").GetTemporalUpscalerInputSize(outputWidth, outputHeight, mode);
    }

    public UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetBackend(denoisingUpscalerBackend, "denoising upscaler").GetDenoisingUpscalerInputSize(outputWidth, outputHeight, mode);
    }

    public TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc)
    {
        return GetBackend(temporalUpscalerBackend, "temporal upscaler").CreateTemporalUpscaler(desc);
    }

    public DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc)
    {
        return GetBackend(denoisingUpscalerBackend, "denoising upscaler").CreateDenoisingUpscaler(desc);
    }

    public FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc)
    {
        return GetBackend(frameGeneratorBackend, "frame generator").CreateFrameGenerator(desc);
    }

    protected override void Destroy()
    {
        for (int i = backends.Count - 1; i >= 0; i--)
        {
            backends[i].Dispose();
        }
    }

    private static IUpscalingBackend? CreateBackend(GraphicsContext context, UpscalingApi api)
    {
        return api switch
        {
            UpscalingApi.DLSS when context.GraphicsApi is GraphicsApi.DirectX12 or GraphicsApi.Vulkan => DLSSBackend.TryCreate(context, out DLSSBackend? dlssBackend) ? dlssBackend : null,
            UpscalingApi.MetalFX when context.GraphicsApi is GraphicsApi.Metal => MetalFXBackend.TryCreate(context, out MetalFXBackend? metalFXBackend) ? metalFXBackend : null,
            _ => default
        };
    }

    private static IUpscalingBackend GetBackend(IUpscalingBackend? backend, string feature)
    {
        return backend ?? throw new NotSupportedException($"This device does not support the {feature} with any of the specified upscaling APIs.");
    }
}

internal interface IUpscalingBackend : IDisposable
{
    UpscalingCapabilities Capabilities { get; }

    UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

    UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

    TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc);

    DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc);

    FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc);
}
