using Zenith.NET.Extensions.Upscaling.DLSS;
using Zenith.NET.Extensions.Upscaling.MetalFX;

namespace Zenith.NET.Extensions.Upscaling;

internal class UpscalingBackend : DisposableObject
{
    private readonly List<UpscalingProvider> providers = [];
    private readonly UpscalingProvider? temporalUpscalerProvider;
    private readonly UpscalingProvider? denoisingUpscalerProvider;
    private readonly UpscalingProvider? frameGeneratorProvider;

    public UpscalingBackend(GraphicsContext context, ReadOnlySpan<UpscalingApi> apis)
    {
        Context = context;

        foreach (UpscalingApi api in apis.ToArray().Distinct())
        {
            if (temporalUpscalerProvider is not null && denoisingUpscalerProvider is not null && frameGeneratorProvider is not null)
            {
                break;
            }

            if (CreateProvider(api) is not UpscalingProvider provider)
            {
                continue;
            }

            temporalUpscalerProvider ??= provider.TemporalUpscalerSupported ? provider : null;
            denoisingUpscalerProvider ??= provider.DenoisingUpscalerSupported ? provider : null;
            frameGeneratorProvider ??= provider.FrameGeneratorSupported ? provider : null;

            if (provider == temporalUpscalerProvider || provider == denoisingUpscalerProvider || provider == frameGeneratorProvider)
            {
                providers.Add(provider);
            }
            else
            {
                provider.Dispose();
            }
        }

        if (providers.Count is 0)
        {
            throw new NotSupportedException("This device does not support any of the specified upscaling APIs.");
        }

        Capabilities = new(temporalUpscalerProvider is not null, denoisingUpscalerProvider is not null, frameGeneratorProvider is not null);
    }

    public GraphicsContext Context { get; }

    public UpscalingCapabilities Capabilities { get; }

    public UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetProvider(temporalUpscalerProvider, "temporal upscaler").GetTemporalUpscalerInputSize(outputWidth, outputHeight, mode);
    }

    public UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetProvider(denoisingUpscalerProvider, "denoising upscaler").GetDenoisingUpscalerInputSize(outputWidth, outputHeight, mode);
    }

    public TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc)
    {
        return GetProvider(temporalUpscalerProvider, "temporal upscaler").CreateTemporalUpscaler(desc);
    }

    public DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc)
    {
        return GetProvider(denoisingUpscalerProvider, "denoising upscaler").CreateDenoisingUpscaler(desc);
    }

    public FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc)
    {
        return GetProvider(frameGeneratorProvider, "frame generator").CreateFrameGenerator(desc);
    }

    protected override void Destroy()
    {
        foreach (UpscalingProvider provider in providers)
        {
            provider.Dispose();
        }
    }

    private UpscalingProvider? CreateProvider(UpscalingApi api)
    {
        return api switch
        {
            UpscalingApi.DLSS when Context.GraphicsApi is GraphicsApi.DirectX12 or GraphicsApi.Vulkan => new DLSSProvider(Context),
            UpscalingApi.MetalFX when Context.GraphicsApi is GraphicsApi.Metal => new MetalFXProvider(Context),
            _ => null
        };
    }

    private static UpscalingProvider GetProvider(UpscalingProvider? provider, string feature)
    {
        return provider ?? throw new NotSupportedException($"This device does not support the {feature} with any of the specified upscaling APIs.");
    }
}
