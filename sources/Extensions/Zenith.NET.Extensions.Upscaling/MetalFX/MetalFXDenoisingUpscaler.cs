using System.Numerics;
using System.Runtime.CompilerServices;
using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXDenoisingUpscaler : DenoisingUpscaler
{
    private readonly MTL4FXTemporalDenoisedScaler scaler;

    public MetalFXDenoisingUpscaler(MetalFXProvider provider, DenoisingUpscalerDesc desc) : base(desc)
    {
        using MTLFXTemporalDenoisedScalerDescriptor descriptor = new()
        {
            ColorTextureFormat = MetalFXFormats.Metal(desc.ColorFormat),
            DepthTextureFormat = MetalFXFormats.Metal(desc.DepthFormat),
            MotionTextureFormat = MetalFXFormats.Metal(desc.MotionVectorsFormat),
            DiffuseAlbedoTextureFormat = MetalFXFormats.Metal(desc.DiffuseAlbedoFormat),
            SpecularAlbedoTextureFormat = MetalFXFormats.Metal(desc.SpecularAlbedoFormat),
            NormalTextureFormat = MetalFXFormats.Metal(desc.NormalsFormat),
            RoughnessTextureFormat = MetalFXFormats.Metal(desc.RoughnessFormat),
            SpecularHitDistanceTextureFormat = MetalFXFormats.Metal(desc.SpecularHitDistanceFormat),
            TransparencyOverlayTextureFormat = MetalFXFormats.Metal(desc.TransparencyOverlayFormat),
            OutputTextureFormat = MetalFXFormats.Metal(desc.OutputFormat),
            InputWidth = desc.InputWidth,
            InputHeight = desc.InputHeight,
            OutputWidth = desc.OutputWidth,
            OutputHeight = desc.OutputHeight,
            IsAutoExposureEnabled = true,
            IsReactiveMaskTextureEnabled = desc.ReactiveMaskFormat is not null,
            ReactiveMaskTextureFormat = MetalFXFormats.Metal(desc.ReactiveMaskFormat),
            IsSpecularHitDistanceTextureEnabled = true,
            IsTransparencyOverlayTextureEnabled = desc.TransparencyOverlayFormat is not null
        };

        scaler = descriptor.MakeTemporalDenoisedScaler(provider.Device, provider.Compiler);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, DenoisingUpscalerArgs args)
    {
        using NSAutoreleasePool _ = new();

        MTL4CommandBuffer mtlCommandBuffer = commandBuffer.Metal();

        scaler.ColorTexture = args.Color.Metal();
        scaler.DepthTexture = args.Depth.Metal();
        scaler.MotionTexture = args.MotionVectors.Metal();
        scaler.DiffuseAlbedoTexture = args.DiffuseAlbedo.Metal();
        scaler.SpecularAlbedoTexture = args.SpecularAlbedo.Metal();
        scaler.NormalTexture = args.Normals.Metal();
        scaler.RoughnessTexture = args.Roughness.Metal();
        scaler.SpecularHitDistanceTexture = args.SpecularHitDistance.Metal();
        scaler.ReactiveMaskTexture = args.ReactiveMask?.Metal() ?? MTLTexture.Null;
        scaler.TransparencyOverlayTexture = args.TransparencyOverlay?.Metal() ?? MTLTexture.Null;
        scaler.OutputTexture = args.Output.Metal();
        scaler.JitterOffsetX = args.JitterOffset.X;
        scaler.JitterOffsetY = args.JitterOffset.Y;
        scaler.MotionVectorScaleX = args.MotionVectorScale.X;
        scaler.MotionVectorScaleY = args.MotionVectorScale.Y;
        scaler.WorldToViewMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.WorldToView);
        scaler.ViewToClipMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.ViewToClip);
        scaler.PreExposure = args.PreExposure;
        scaler.ShouldResetHistory = args.Reset;
        scaler.IsDepthReversed = Desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed);
        scaler.Encode(mtlCommandBuffer);

        MetalFXProvider.EncodeBarrier(mtlCommandBuffer);
    }

    protected override void Destroy()
    {
        scaler?.Dispose();
    }
}
