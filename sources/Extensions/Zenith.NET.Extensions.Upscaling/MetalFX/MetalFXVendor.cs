namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXVendor(GraphicsContext context) : Vendor(context)
{
    public override UpscalingCapabilities Capabilities => throw new NotImplementedException();

    public override (uint Width, uint Height) GetSuperResolutionRenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        throw new NotImplementedException();
    }

    public override (uint Width, uint Height) GetRayReconstructionRenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        throw new NotImplementedException();
    }

    public override SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
    {
        throw new NotImplementedException();
    }

    public override RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
    {
        throw new NotImplementedException();
    }

    public override FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        throw new NotImplementedException();
    }
}
