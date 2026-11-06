namespace Zenith.NET.Extensions.Upscaling.FSR;

internal class FSRVendor : Vendor
{
    public FSRVendor(GraphicsContext context)
    {
        throw new NotImplementedException("The FSR backend is not implemented.");
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
