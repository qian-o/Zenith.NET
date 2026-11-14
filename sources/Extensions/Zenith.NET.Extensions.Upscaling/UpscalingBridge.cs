using Zenith.NET.Extensions.Upscaling.DLSS;
using Zenith.NET.Extensions.Upscaling.FSR;
using Zenith.NET.Extensions.Upscaling.MetalFX;

namespace Zenith.NET.Extensions.Upscaling;

internal class UpscalingBridge : DisposableObject
{
    private readonly List<Vendor> vendors = [];

    private Vendor? superResolutionVendor;
    private Vendor? rayReconstructionVendor;
    private Vendor? frameGenerationVendor;

    public UpscalingBridge(GraphicsContext context)
    {
        Context = context;

        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
            case GraphicsApi.Vulkan:
                Register(new DLSSVendor(context));

                if (superResolutionVendor is null || rayReconstructionVendor is null || frameGenerationVendor is null)
                {
                    Register(new FSRVendor(context));
                }
                break;

            case GraphicsApi.Metal:
                Register(new MetalFXVendor(context));
                break;
        }

        Capabilities = new(superResolutionVendor is not null, rayReconstructionVendor is not null, frameGenerationVendor is not null);
    }

    public GraphicsContext Context { get; }

    public UpscalingCapabilities Capabilities { get; }

    public SuperResolutionDesc CreateSuperResolutionDesc(PixelFormat format, uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        (uint renderWidth, uint renderHeight) = GetVendor(superResolutionVendor, "super resolution").GetSuperResolutionRenderSize(outputWidth, outputHeight, quality);

        return new()
        {
            Format = format,
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            OutputWidth = outputWidth,
            OutputHeight = outputHeight
        };
    }

    public RayReconstructionDesc CreateRayReconstructionDesc(PixelFormat format, uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        (uint renderWidth, uint renderHeight) = GetVendor(rayReconstructionVendor, "ray reconstruction").GetRayReconstructionRenderSize(outputWidth, outputHeight, quality);

        return new()
        {
            Format = format,
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            OutputWidth = outputWidth,
            OutputHeight = outputHeight
        };
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
        for (int i = vendors.Count - 1; i >= 0; i--)
        {
            vendors[i].Dispose();
        }
        vendors.Clear();
    }

    private void Register(Vendor vendor)
    {
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

    private static Vendor GetVendor(Vendor? vendor, string feature)
    {
        return vendor ?? throw new NotSupportedException($"This device does not support {feature}.");
    }
}
