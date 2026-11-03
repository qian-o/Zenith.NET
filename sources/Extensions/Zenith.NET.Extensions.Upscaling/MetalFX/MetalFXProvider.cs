using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXProvider(GraphicsContext context) : UpscalingProvider(context)
{
    public readonly MTLDevice Device = new(context.GetNativeObject(NativeObjectType.MTLDevice), NativeObjectOwnership.Borrowed);

    public MTL4Compiler Compiler = MTL4Compiler.Null;

    private static bool IsSystemSupported => OperatingSystem.IsMacOSVersionAtLeast(27) || OperatingSystem.IsIOSVersionAtLeast(27) || OperatingSystem.IsTvOSVersionAtLeast(27, 1);

    public override bool TemporalUpscalerSupported => IsSystemSupported && MTLFXTemporalScalerDescriptor.SupportsMetal4FX(Device);

    public override bool DenoisingUpscalerSupported => IsSystemSupported && MTLFXTemporalDenoisedScalerDescriptor.SupportsMetal4FX(Device);

    public override bool FrameGeneratorSupported => IsSystemSupported && MTLFXFrameInterpolatorDescriptor.SupportsMetal4FX(Device);

    public override UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetInputSize(outputWidth, outputHeight, MetalFXFormats.Scale(mode), MTLFXTemporalScalerDescriptor.SupportedInputContentMinScale(Device), MTLFXTemporalScalerDescriptor.SupportedInputContentMaxScale(Device));
    }

    public override UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetInputSize(outputWidth, outputHeight, MetalFXFormats.Scale(mode), MTLFXTemporalDenoisedScalerDescriptor.SupportedInputContentMinScale(Device), MTLFXTemporalDenoisedScalerDescriptor.SupportedInputContentMaxScale(Device));
    }

    public override TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc)
    {
        return new MetalFXTemporalUpscaler(this, desc);
    }

    public override DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc)
    {
        return new MetalFXDenoisingUpscaler(this, desc);
    }

    public override FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc)
    {
        return new MetalFXFrameGenerator(this, desc);
    }

    protected override void Initialize()
    {
        Compiler = Device.MakeCompiler(new(), out NSError error);
        error.Success();
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

    private static UpscalingInputSize GetInputSize(uint outputWidth, uint outputHeight, float scale, float minScale, float maxScale)
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
