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

    public SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
    {
        return superResolutionVendor!.CreateSuperResolution(desc);
    }

    public RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
    {
        return rayReconstructionVendor!.CreateRayReconstruction(desc);
    }

    public FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
    {
        return frameGenerationVendor!.CreateFrameGeneration(desc);
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
        superResolutionVendor ??= vendor.Capabilities.SuperResolutionSupported ? vendor : null;
        rayReconstructionVendor ??= vendor.Capabilities.RayReconstructionSupported ? vendor : null;
        frameGenerationVendor ??= vendor.Capabilities.FrameGenerationSupported ? vendor : null;

        if (vendor == superResolutionVendor || vendor == rayReconstructionVendor || vendor == frameGenerationVendor)
        {
            vendors.Add(vendor);
        }
        else
        {
            vendor.Dispose();
        }
    }

    public static Texture CreateOutput(GraphicsContext context, PixelFormat format, uint width, uint height)
    {
        return context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment | TextureUsages.TransferSrc | TextureUsages.TransferDst
        });
    }
}

internal abstract class Vendor(GraphicsContext context) : DisposableObject
{
    public GraphicsContext Context { get; } = context;

    public abstract UpscalingCapabilities Capabilities { get; }

    public abstract SuperResolution CreateSuperResolution(SuperResolutionDesc desc);

    public abstract RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc);

    public abstract FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc);
}
