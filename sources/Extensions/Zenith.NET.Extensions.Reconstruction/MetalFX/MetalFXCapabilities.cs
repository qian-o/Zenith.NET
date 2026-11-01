using Metal.NET;

namespace Zenith.NET.Extensions.Reconstruction.MetalFX;

internal class MetalFXCapabilities(MetalFXBackend backend) : ReconstructionCapabilities
{
    private static bool IsSystemSupported => OperatingSystem.IsMacOSVersionAtLeast(27) || OperatingSystem.IsIOSVersionAtLeast(27) || OperatingSystem.IsTvOSVersionAtLeast(27, 1);

    public override bool TemporalUpscalerSupported { get; } = IsSystemSupported && MTLFXTemporalScalerDescriptor.SupportsMetal4FX(backend.Device);

    public override bool DenoisingUpscalerSupported { get; } = IsSystemSupported && MTLFXTemporalDenoisedScalerDescriptor.SupportsMetal4FX(backend.Device);

    public override bool FrameGeneratorSupported { get; } = IsSystemSupported && MTLFXFrameInterpolatorDescriptor.SupportsMetal4FX(backend.Device);
}
