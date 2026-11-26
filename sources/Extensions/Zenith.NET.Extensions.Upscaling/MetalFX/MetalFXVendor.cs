using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXVendor : Vendor
{
    public MTLDevice Device;

    public MTL4Compiler Compiler;

    public MetalFXVendor(GraphicsContext context) : base(context)
    {
        Device = context.Metal();

        using MTL4CompilerDescriptor descriptor = new();
        Compiler = Device.MakeCompiler(descriptor, out NSError error);
        error.Success();

        SuperResolutionSupported = MTLFXTemporalScalerDescriptor.SupportsMetal4FX(Device);
        RayReconstructionSupported = MTLFXTemporalDenoisedScalerDescriptor.SupportsMetal4FX(Device);
        FrameGenerationSupported = MTLFXFrameInterpolatorDescriptor.SupportsMetal4FX(Device);
    }

    public override bool SuperResolutionSupported { get; }

    public override bool RayReconstructionSupported { get; }

    public override bool FrameGenerationSupported { get; }

    public override SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
    {
        return new MetalFXSuperResolution(this, desc);
    }

    public override RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
    {
        return new MetalFXRayReconstruction(this, desc);
    }

    public override FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
    {
        return new MetalFXFrameGeneration(this, desc);
    }

    protected override void Destroy()
    {
        Compiler.Dispose();
    }
}
