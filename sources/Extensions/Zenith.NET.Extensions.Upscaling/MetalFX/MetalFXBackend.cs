using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXBackend : DisposableObject, IUpscalingBackend
{
    private readonly (float Min, float Max) temporalScale;
    private readonly (float Min, float Max) denoisingScale;

    public readonly MTLDevice Device;

    public readonly MTL4Compiler Compiler;

    private MetalFXBackend(MTLDevice device, UpscalingCapabilities capabilities)
    {
        Device = device;
        Capabilities = capabilities;

        if (capabilities.TemporalUpscalerSupported)
        {
            temporalScale = (MTLFXTemporalScalerDescriptor.SupportedInputContentMinScale(device), MTLFXTemporalScalerDescriptor.SupportedInputContentMaxScale(device));
        }

        if (capabilities.DenoisingUpscalerSupported)
        {
            denoisingScale = (MTLFXTemporalDenoisedScalerDescriptor.SupportedInputContentMinScale(device), MTLFXTemporalDenoisedScalerDescriptor.SupportedInputContentMaxScale(device));
        }

        Compiler = device.MakeCompiler(new(), out NSError error);
        error.Success();
    }

    public UpscalingCapabilities Capabilities { get; }

    public UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetInputSize(outputWidth, outputHeight, MetalFXFormats.Scale(mode), temporalScale.Min, temporalScale.Max);
    }

    public UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetInputSize(outputWidth, outputHeight, MetalFXFormats.Scale(mode), denoisingScale.Min, denoisingScale.Max);
    }

    public TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc)
    {
        return new MetalFXTemporalUpscaler(this, desc);
    }

    public DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc)
    {
        return new MetalFXDenoisingUpscaler(this, desc);
    }

    public FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc)
    {
        return new MetalFXFrameGenerator(this, desc);
    }

    protected override void Destroy()
    {
        Compiler?.Dispose();
    }

    public static bool TryCreate(GraphicsContext context, [NotNullWhen(true)] out MetalFXBackend? backend)
    {
        backend = null;

        if (!OperatingSystem.IsMacOSVersionAtLeast(27) && !OperatingSystem.IsIOSVersionAtLeast(27) && !OperatingSystem.IsTvOSVersionAtLeast(27, 1))
        {
            return false;
        }

        MTLDevice device = new(context.GetNativeObject(NativeObjectType.MTLDevice), NativeObjectOwnership.Borrowed);
        UpscalingCapabilities capabilities = new(MTLFXTemporalScalerDescriptor.SupportsMetal4FX(device), MTLFXTemporalDenoisedScalerDescriptor.SupportsMetal4FX(device), MTLFXFrameInterpolatorDescriptor.SupportsMetal4FX(device));
        if (!capabilities.TemporalUpscalerSupported && !capabilities.DenoisingUpscalerSupported && !capabilities.FrameGeneratorSupported)
        {
            return false;
        }

        MetalFXBackend candidate = new(device, capabilities);
        if (candidate.Compiler.IsNull)
        {
            Debug.WriteLine("MetalFX compiler creation failed.");

            candidate.Dispose();

            return false;
        }

        backend = candidate;

        return true;
    }

    public static void EncodeBarrier(MTL4CommandBuffer commandBuffer)
    {
        MTL4ComputeCommandEncoder encoder = commandBuffer.MakeComputeCommandEncoder();

        encoder.BarrierAfterStages(MTLStages.All, MTLStages.All, MTL4VisibilityOptions.Device);
        encoder.EndEncoding();
    }

    private static UpscalingInputSize GetInputSize(uint outputWidth, uint outputHeight, float scale, float minScale, float maxScale)
    {
        if (scale < minScale || scale > maxScale)
        {
            Debug.WriteLine($"MetalFX does not support the requested upscaling scale: {scale}");

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
