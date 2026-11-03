using Zenith.NET.Extensions.Upscaling.DLSS;
using Zenith.NET.Extensions.Upscaling.MetalFX;

namespace Zenith.NET.Extensions.Upscaling;

internal class UpscalingBridge : DisposableObject
{
    private readonly List<Vendor> vendors = [];
    private readonly Vendor? temporalUpscalerVendor;
    private readonly Vendor? denoisingUpscalerVendor;
    private readonly Vendor? frameGeneratorVendor;

    public UpscalingBridge(GraphicsContext context, ReadOnlySpan<UpscalingApi> apis)
    {
        foreach (UpscalingApi api in apis.ToArray().Distinct())
        {
            if (temporalUpscalerVendor is not null && denoisingUpscalerVendor is not null && frameGeneratorVendor is not null)
            {
                break;
            }

            if (CreateVendor(context, api) is not Vendor vendor)
            {
                continue;
            }

            UpscalingCapabilities capabilities = vendor.Capabilities;
            temporalUpscalerVendor ??= capabilities.TemporalUpscalerSupported ? vendor : null;
            denoisingUpscalerVendor ??= capabilities.DenoisingUpscalerSupported ? vendor : null;
            frameGeneratorVendor ??= capabilities.FrameGeneratorSupported ? vendor : null;

            if (vendor == temporalUpscalerVendor || vendor == denoisingUpscalerVendor || vendor == frameGeneratorVendor)
            {
                vendors.Add(vendor);
            }
            else
            {
                vendor.Dispose();
            }
        }

        if (vendors.Count is 0)
        {
            throw new NotSupportedException("This device does not support any of the specified upscaling APIs.");
        }

        Capabilities = new(temporalUpscalerVendor is not null, denoisingUpscalerVendor is not null, frameGeneratorVendor is not null);
    }

    public UpscalingCapabilities Capabilities { get; }

    public UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetVendor(temporalUpscalerVendor, "temporal upscaler").GetTemporalUpscalerInputSize(outputWidth, outputHeight, mode);
    }

    public UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetVendor(denoisingUpscalerVendor, "denoising upscaler").GetDenoisingUpscalerInputSize(outputWidth, outputHeight, mode);
    }

    public TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc)
    {
        return GetVendor(temporalUpscalerVendor, "temporal upscaler").CreateTemporalUpscaler(desc);
    }

    public DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc)
    {
        return GetVendor(denoisingUpscalerVendor, "denoising upscaler").CreateDenoisingUpscaler(desc);
    }

    public FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc)
    {
        return GetVendor(frameGeneratorVendor, "frame generator").CreateFrameGenerator(desc);
    }

    protected override void Destroy()
    {
        for (int i = vendors.Count - 1; i >= 0; i--)
        {
            vendors[i].Dispose();
        }
    }

    private static Vendor? CreateVendor(GraphicsContext context, UpscalingApi api)
    {
        return api switch
        {
            UpscalingApi.DLSS when context.GraphicsApi is GraphicsApi.DirectX12 or GraphicsApi.Vulkan => DLSSVendor.TryCreate(context, out DLSSVendor? dlssVendor) ? dlssVendor : null,
            UpscalingApi.MetalFX when context.GraphicsApi is GraphicsApi.Metal => MetalFXVendor.TryCreate(context, out MetalFXVendor? metalFXVendor) ? metalFXVendor : null,
            _ => default
        };
    }

    private static Vendor GetVendor(Vendor? vendor, string feature)
    {
        return vendor ?? throw new NotSupportedException($"This device does not support the {feature} with any of the specified upscaling APIs.");
    }
}

internal abstract class Vendor : DisposableObject
{
    public abstract UpscalingCapabilities Capabilities { get; }

    public abstract UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

    public abstract UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

    public abstract TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc);

    public abstract DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc);

    public abstract FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc);
}
