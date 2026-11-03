using Metal.NET;

namespace Zenith.NET.Extensions.Reconstruction.MetalFX;

internal class MetalFXBackend(GraphicsContext context, string dataPath) : ReconstructionBackend(context, dataPath)
{
    public readonly MTLDevice Device = new(context.GetNativeObject(NativeObjectType.MTLDevice), NativeObjectOwnership.Borrowed);

    public MTL4Compiler Compiler = MTL4Compiler.Null;

    protected override void Initialize(string dataPath, out ReconstructionCapabilities capabilities)
    {
        Compiler = Device.MakeCompiler(new(), out NSError error);
        error.Success();

        capabilities = new MetalFXCapabilities(this);
    }

    protected override UpscalerInputSize GetTemporalUpscalerInputSizeImpl(uint outputWidth, uint outputHeight, UpscalerMode mode)
    {
        if (!Capabilities.TemporalUpscalerSupported)
        {
            return default;
        }

        return GetInputSize(outputWidth, outputHeight, MetalFXFormats.Scale(mode), MTLFXTemporalScalerDescriptor.SupportedInputContentMinScale(Device), MTLFXTemporalScalerDescriptor.SupportedInputContentMaxScale(Device));
    }

    protected override UpscalerInputSize GetDenoisingUpscalerInputSizeImpl(uint outputWidth, uint outputHeight, UpscalerMode mode)
    {
        if (!Capabilities.DenoisingUpscalerSupported)
        {
            return default;
        }

        return GetInputSize(outputWidth, outputHeight, MetalFXFormats.Scale(mode), MTLFXTemporalDenoisedScalerDescriptor.SupportedInputContentMinScale(Device), MTLFXTemporalDenoisedScalerDescriptor.SupportedInputContentMaxScale(Device));
    }

    protected override TemporalUpscaler CreateTemporalUpscalerImpl(TemporalUpscalerDesc desc)
    {
        return new MetalFXTemporalUpscaler(this, desc);
    }

    protected override DenoisingUpscaler CreateDenoisingUpscalerImpl(DenoisingUpscalerDesc desc)
    {
        return new MetalFXDenoisingUpscaler(this, desc);
    }

    protected override FrameGenerator CreateFrameGeneratorImpl(FrameGeneratorDesc desc)
    {
        return new MetalFXFrameGenerator(this, desc);
    }

    protected override void Destroy()
    {
        Compiler.Dispose();
    }

    public static void EncodeBarrier(MTL4CommandBuffer commandBuffer)
    {
        // MetalFX ends its own encoders without ordering later passes, so a producer barrier makes them wait for it.
        MTL4ComputeCommandEncoder encoder = commandBuffer.MakeComputeCommandEncoder();
        encoder.BarrierAfterStages(MTLStages.All, MTLStages.All, MTL4VisibilityOptions.Device);
        encoder.EndEncoding();
    }

    private static UpscalerInputSize GetInputSize(uint outputWidth, uint outputHeight, float scale, float minScale, float maxScale)
    {
        if (scale < minScale || scale > maxScale)
        {
            return default;
        }

        return new(GetInputLength(outputWidth, scale, minScale, maxScale), GetInputLength(outputHeight, scale, minScale, maxScale));
    }

    private static uint GetInputLength(uint outputLength, float scale, float minScale, float maxScale)
    {
        uint inputLength = (uint)MathF.Floor((outputLength / scale) + 0.5f);

        return Math.Clamp(inputLength, (uint)MathF.Ceiling(outputLength / maxScale), (uint)MathF.Floor(outputLength / minScale));
    }
}
