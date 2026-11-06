namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSVendor : Vendor
{
    public DLSSVendor(GraphicsContext context)
    {
        throw new NotImplementedException("The DLSS backend is not implemented.");
    }

    public override UpscalingCapabilities Capabilities => throw new NotImplementedException();

    public override UpscalingInputSize GetSuperResolutionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        throw new NotImplementedException();
    }

    public override UpscalingInputSize GetRayReconstructionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
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
    }
}
