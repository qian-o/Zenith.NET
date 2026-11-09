using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXVendor : Vendor
{
    private readonly (float Min, float Max) superResolutionScale;
    private readonly (float Min, float Max) rayReconstructionScale;

    public readonly MTLDevice Device = MTLDevice.Null;

    public readonly MTL4Compiler Compiler = MTL4Compiler.Null;

    public MetalFXVendor(GraphicsContext context)
    {
        if (!OperatingSystem.IsMacOSVersionAtLeast(27) && !OperatingSystem.IsIOSVersionAtLeast(27) && !OperatingSystem.IsTvOSVersionAtLeast(27, 1))
        {
            Capabilities = new(false, false, false);

            return;
        }

        Device = new(context.GetNativeObject(NativeObjectType.MTLDevice), NativeObjectOwnership.Borrowed);
        Capabilities = new(MTLFXTemporalScalerDescriptor.SupportsMetal4FX(Device),
                           MTLFXTemporalDenoisedScalerDescriptor.SupportsMetal4FX(Device),
                           MTLFXFrameInterpolatorDescriptor.SupportsMetal4FX(Device));

        if (!Capabilities.SuperResolutionSupported && !Capabilities.RayReconstructionSupported && !Capabilities.FrameGenerationSupported)
        {
            return;
        }

        Compiler = Device.MakeCompiler(new(), out NSError error);
        error.Success();

        if (Compiler.IsNull)
        {
            Capabilities = new(false, false, false);

            return;
        }

        if (Capabilities.SuperResolutionSupported)
        {
            superResolutionScale = (MTLFXTemporalScalerDescriptor.SupportedInputContentMinScale(Device), MTLFXTemporalScalerDescriptor.SupportedInputContentMaxScale(Device));
        }

        if (Capabilities.RayReconstructionSupported)
        {
            rayReconstructionScale = (MTLFXTemporalDenoisedScalerDescriptor.SupportedInputContentMinScale(Device), MTLFXTemporalDenoisedScalerDescriptor.SupportedInputContentMaxScale(Device));
        }
    }

    public override UpscalingCapabilities Capabilities { get; }

    public override UpscalingInputSize GetSuperResolutionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetInputSize(outputWidth, outputHeight, UpscalingHelper.Scale(mode), superResolutionScale.Min, superResolutionScale.Max);
    }

    public override UpscalingInputSize GetRayReconstructionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        if (mode is UpscalingMode.Native)
        {
            return default;
        }

        return GetInputSize(outputWidth, outputHeight, UpscalingHelper.Scale(mode), rayReconstructionScale.Min, rayReconstructionScale.Max);
    }

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

    public static void EncodeBarrier(MTL4CommandBuffer commandBuffer)
    {
        MTL4ComputeCommandEncoder encoder = commandBuffer.MakeComputeCommandEncoder();

        encoder.BarrierAfterStages(MTLStages.All, MTLStages.All, MTL4VisibilityOptions.Device);
        encoder.EndEncoding();
    }

    private static UpscalingInputSize GetInputSize(uint outputWidth, uint outputHeight, float scale, float minScale, float maxScale)
    {
        return scale < minScale || scale > maxScale ? default : new((uint)MathF.Floor((outputWidth / scale) + 0.5f), (uint)MathF.Floor((outputHeight / scale) + 0.5f));
    }
}
