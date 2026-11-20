using Zenith.NET.Extensions.Upscaling.DLSS;
using Zenith.NET.Extensions.Upscaling.FSR;
using Zenith.NET.Extensions.Upscaling.MetalFX;

namespace Zenith.NET.Extensions.Upscaling;

internal partial class UpscalingBridge : DisposableObject
{
    private readonly List<Vendor> vendors = [];

    private Vendor? superResolution;
    private Vendor? rayReconstruction;
    private Vendor? frameGeneration;

    public UpscalingBridge(GraphicsContext context)
    {
        Context = context;

        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
            case GraphicsApi.Vulkan:
                Register(new DLSSVendor(context));

                if (superResolution is null || rayReconstruction is null || frameGeneration is null)
                {
                    Register(new FSRVendor(context));
                }
                break;

            case GraphicsApi.Metal:
                Register(new MetalFXVendor(context));
                break;
        }

        Capabilities = new(superResolution is not null, rayReconstruction is not null, frameGeneration is not null);
    }

    public GraphicsContext Context { get; }

    public UpscalingCapabilities Capabilities { get; }

    public SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
    {
        return superResolution!.CreateSuperResolution(desc);
    }

    public RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
    {
        return rayReconstruction!.CreateRayReconstruction(desc);
    }

    public FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
    {
        return frameGeneration!.CreateFrameGeneration(desc);
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
        superResolution ??= vendor.Capabilities.SuperResolutionSupported ? vendor : null;
        rayReconstruction ??= vendor.Capabilities.RayReconstructionSupported ? vendor : null;
        frameGeneration ??= vendor.Capabilities.FrameGenerationSupported ? vendor : null;

        if (vendor == superResolution || vendor == rayReconstruction || vendor == frameGeneration)
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

    public static ComputePipeline CreatePipeline(GraphicsContext context, ShaderDesc desc)
    {
        using Shader shader = context.CreateShader(desc);

        return context.CreateComputePipeline(new() { ComputeShader = shader });
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
