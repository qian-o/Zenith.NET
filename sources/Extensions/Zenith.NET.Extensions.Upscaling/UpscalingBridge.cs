using Zenith.NET.Extensions.Upscaling.DLSS;
using Zenith.NET.Extensions.Upscaling.FSR;
using Zenith.NET.Extensions.Upscaling.MetalFX;

namespace Zenith.NET.Extensions.Upscaling;

internal class UpscalingBridge : DisposableObject
{
    private readonly List<Vendor> vendors = [];
    private readonly Vendor? superResolutionVendor;
    private readonly Vendor? rayReconstructionVendor;
    private readonly Vendor? frameGenerationVendor;

    public UpscalingBridge(GraphicsContext context, ReadOnlySpan<UpscalingApi> apis)
    {
        Context = context;

        foreach (UpscalingApi api in apis.ToArray().Distinct())
        {
            if (superResolutionVendor is not null && rayReconstructionVendor is not null && frameGenerationVendor is not null)
            {
                break;
            }

            if (CreateVendor(context, api) is not Vendor vendor)
            {
                continue;
            }

            UpscalingCapabilities capabilities = vendor.Capabilities;
            superResolutionVendor ??= capabilities.SuperResolutionSupported ? vendor : null;
            rayReconstructionVendor ??= capabilities.RayReconstructionSupported ? vendor : null;
            frameGenerationVendor ??= capabilities.FrameGenerationSupported ? vendor : null;

            if (vendor == superResolutionVendor || vendor == rayReconstructionVendor || vendor == frameGenerationVendor)
            {
                vendors.Add(vendor);
            }
            else
            {
                vendor.Dispose();
            }
        }

        Capabilities = new(superResolutionVendor is not null, rayReconstructionVendor is not null, frameGenerationVendor is not null);
    }

    public GraphicsContext Context { get; }

    public UpscalingCapabilities Capabilities { get; }

    public UpscalingInputSize GetSuperResolutionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetVendor(superResolutionVendor, "super resolution").GetSuperResolutionInputSize(outputWidth, outputHeight, mode);
    }

    public UpscalingInputSize GetRayReconstructionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetVendor(rayReconstructionVendor, "ray reconstruction").GetRayReconstructionInputSize(outputWidth, outputHeight, mode);
    }

    public SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
    {
        return GetVendor(superResolutionVendor, "super resolution").CreateSuperResolution(desc);
    }

    public RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
    {
        return GetVendor(rayReconstructionVendor, "ray reconstruction").CreateRayReconstruction(desc);
    }

    public FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
    {
        return GetVendor(frameGenerationVendor, "frame generation").CreateFrameGeneration(desc);
    }

    public FrameGenerationSwapChain CreateFrameGenerationSwapChain(FrameGenerationSwapChainDesc desc)
    {
        return new(Context, desc);
    }

    protected override void Destroy()
    {
        for (int i = 0; i < vendors.Count; i++)
        {
            vendors[i].Dispose();
        }
    }

    private static Vendor? CreateVendor(GraphicsContext context, UpscalingApi api)
    {
        return api switch
        {
            UpscalingApi.DLSS when context.GraphicsApi is GraphicsApi.DirectX12 or GraphicsApi.Vulkan => new DLSSVendor(context),
            UpscalingApi.FSR when context.GraphicsApi is GraphicsApi.DirectX12 or GraphicsApi.Vulkan => new FSRVendor(context),
            UpscalingApi.MetalFX when context.GraphicsApi is GraphicsApi.Metal => new MetalFXVendor(context),
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

    public abstract UpscalingInputSize GetSuperResolutionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

    public abstract UpscalingInputSize GetRayReconstructionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

    public abstract SuperResolution CreateSuperResolution(SuperResolutionDesc desc);

    public abstract RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc);

    public abstract FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc);
}
