using Zenith.NET.Extensions.Upscaling.DLSS;
using Zenith.NET.Extensions.Upscaling.MetalFX;

namespace Zenith.NET.Extensions.Upscaling;

internal class UpscalingBackend : DisposableObject
{
    private readonly List<Provider> providers = [];
    private readonly Provider? temporalUpscalerProvider;
    private readonly Provider? denoisingUpscalerProvider;
    private readonly Provider? frameGeneratorProvider;

    public UpscalingBackend(GraphicsContext context, ReadOnlySpan<UpscalingProvider> providers)
    {
        Context = context;

        foreach (UpscalingProvider value in providers.ToArray().Distinct())
        {
            if (temporalUpscalerProvider is not null && denoisingUpscalerProvider is not null && frameGeneratorProvider is not null)
            {
                break;
            }

            if (CreateProvider(value) is not Provider provider)
            {
                continue;
            }

            temporalUpscalerProvider ??= provider.Capabilities.TemporalUpscalerSupported ? provider : null;
            denoisingUpscalerProvider ??= provider.Capabilities.DenoisingUpscalerSupported ? provider : null;
            frameGeneratorProvider ??= provider.Capabilities.FrameGeneratorSupported ? provider : null;

            if (provider == temporalUpscalerProvider || provider == denoisingUpscalerProvider || provider == frameGeneratorProvider)
            {
                this.providers.Add(provider);
            }
            else
            {
                provider.Dispose();
            }
        }

        if (this.providers.Count is 0)
        {
            throw new NotSupportedException("This device does not support any of the specified upscaling providers.");
        }

        Capabilities = new UpscalingBackendCapabilities(temporalUpscalerProvider, denoisingUpscalerProvider, frameGeneratorProvider);
    }

    public GraphicsContext Context { get; }

    public UpscalingCapabilities Capabilities { get; }

    public UpscalerInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalerMode mode)
    {
        return GetProvider(temporalUpscalerProvider, "temporal upscaler").GetTemporalUpscalerInputSize(outputWidth, outputHeight, mode);
    }

    public UpscalerInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalerMode mode)
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
        foreach (Provider provider in providers)
        {
            provider.Dispose();
        }
    }

    private Provider? CreateProvider(UpscalingProvider provider)
    {
        return provider switch
        {
            UpscalingProvider.DLSS when Context.GraphicsApi is GraphicsApi.DirectX12 or GraphicsApi.Vulkan => new DLSSProvider(Context),
            UpscalingProvider.MetalFX when Context.GraphicsApi is GraphicsApi.Metal => new MetalFXProvider(Context),
            _ => null
        };
    }

    private static Provider GetProvider(Provider? provider, string feature)
    {
        return provider ?? throw new NotSupportedException($"This device does not support the {feature} with any of the specified upscaling providers.");
    }
}
