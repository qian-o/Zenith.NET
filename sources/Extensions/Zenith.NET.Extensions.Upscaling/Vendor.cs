namespace Zenith.NET.Extensions.Upscaling;

internal abstract class Vendor(GraphicsContext context) : DisposableObject
{
    public GraphicsContext Context { get; } = context;

    public abstract UpscalingCapabilities Capabilities { get; }

    public abstract (uint Width, uint Height) GetSuperResolutionRenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality);

    public abstract (uint Width, uint Height) GetRayReconstructionRenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality);

    public abstract SuperResolution CreateSuperResolution(SuperResolutionDesc desc);

    public abstract RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc);

    public abstract FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc);

    public static Texture CreateOutput(GraphicsContext context, PixelFormat format, uint width, uint height)
    {
        return context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment | TextureUsages.TransferSrc | TextureUsages.TransferDst
        });
    }
}
