using System.Numerics;
using System.Runtime.CompilerServices;
using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXRayReconstruction : RayReconstruction
{
    public MTL4FXTemporalDenoisedScaler Scaler;

    public MetalFXRayReconstruction(MetalFXVendor vendor, RayReconstructionDesc desc) : base(desc)
    {
        MTLFXTemporalDenoisedScalerDescriptor descriptor = new()
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
            IsAutoExposureEnabled = desc.Flags.HasFlag(UpscalingInputFlags.AutoExposure),
            IsReactiveMaskTextureEnabled = desc.ReactiveMaskFormat is not null,
            ReactiveMaskTextureFormat = MetalFXFormats.Metal(desc.ReactiveMaskFormat),
            IsSpecularHitDistanceTextureEnabled = true,
            IsTransparencyOverlayTextureEnabled = desc.TransparencyOverlayFormat is not null
        };

        Scaler = descriptor.MakeTemporalDenoisedScaler(vendor.Device, vendor.Compiler);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        using NSAutoreleasePool _ = new();

        MTL4CommandBuffer mtlCommandBuffer = commandBuffer.Metal();

        Scaler.ColorTexture = args.Color.Metal();
        Scaler.DepthTexture = args.Depth.Metal();
        Scaler.MotionTexture = args.MotionVectors.Metal();
        Scaler.DiffuseAlbedoTexture = args.DiffuseAlbedo.Metal();
        Scaler.SpecularAlbedoTexture = args.SpecularAlbedo.Metal();
        Scaler.NormalTexture = args.Normals.Metal();
        Scaler.RoughnessTexture = args.Roughness.Metal();
        Scaler.SpecularHitDistanceTexture = args.SpecularHitDistance.Metal();
        Scaler.ExposureTexture = args.Exposure?.Metal() ?? MTLTexture.Null;
        Scaler.ReactiveMaskTexture = args.ReactiveMask?.Metal() ?? MTLTexture.Null;
        Scaler.TransparencyOverlayTexture = args.TransparencyOverlay?.Metal() ?? MTLTexture.Null;
        Scaler.OutputTexture = args.Output.Metal();
        Scaler.JitterOffsetX = args.JitterOffset.X;
        Scaler.JitterOffsetY = args.JitterOffset.Y;
        Scaler.MotionVectorScaleX = Desc.InputWidth;
        Scaler.MotionVectorScaleY = Desc.InputHeight;
        Scaler.WorldToViewMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.WorldToView);
        Scaler.ViewToClipMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.ViewToClip);
        Scaler.PreExposure = args.PreExposure;
        Scaler.ShouldResetHistory = args.Reset;
        Scaler.IsDepthReversed = Desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed);
        Scaler.Encode(mtlCommandBuffer);

        MetalFXVendor.EncodeBarrier(mtlCommandBuffer);
    }

    protected override void Destroy()
    {
        Scaler.Dispose();
    }
}
