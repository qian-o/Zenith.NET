using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXRayReconstruction(MetalFXVendor vendor, RayReconstructionDesc desc) : RayReconstruction(vendor.Context, desc)
{
    private MTL4FXTemporalDenoisedScaler scaler;

    protected override void Build(RayReconstructionArgs args)
    {
        using NSAutoreleasePool _ = new();

        using MTLFXTemporalDenoisedScalerDescriptor descriptor = new()
        {
            ColorTextureFormat = MetalFXFormats.Metal(args.Color.Desc.Format),
            DepthTextureFormat = MetalFXFormats.Metal(args.Depth.Desc.Format),
            MotionTextureFormat = MetalFXFormats.Metal(args.MotionVectors.Desc.Format),
            DiffuseAlbedoTextureFormat = MetalFXFormats.Metal(args.DiffuseAlbedo.Desc.Format),
            SpecularAlbedoTextureFormat = MetalFXFormats.Metal(args.SpecularAlbedo.Desc.Format),
            NormalTextureFormat = MetalFXFormats.Metal(args.Normals.Desc.Format),
            RoughnessTextureFormat = MetalFXFormats.Metal(args.Roughness.Desc.Format),
            SpecularHitDistanceTextureFormat = MetalFXFormats.Metal(args.SpecularHitDistance?.Desc.Format),
            TransparencyOverlayTextureFormat = MetalFXFormats.Metal(args.TransparencyOverlay?.Desc.Format),
            OutputTextureFormat = MetalFXFormats.Metal(Desc.Format),
            InputWidth = Desc.RenderWidth,
            InputHeight = Desc.RenderHeight,
            OutputWidth = Desc.OutputWidth,
            OutputHeight = Desc.OutputHeight,
            RequiresSynchronousInitialization = true,
            IsAutoExposureEnabled = args.Exposure is null,
            IsReactiveMaskTextureEnabled = args.ReactiveMask is not null,
            ReactiveMaskTextureFormat = MetalFXFormats.Metal(args.ReactiveMask?.Desc.Format),
            IsSpecularHitDistanceTextureEnabled = args.SpecularHitDistance is not null,
            IsTransparencyOverlayTextureEnabled = args.TransparencyOverlay is not null
        };

        scaler.Dispose();
        scaler = descriptor.MakeTemporalDenoisedScaler(vendor.Device, vendor.Compiler);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        using NSAutoreleasePool _ = new();

        commandBuffer.Transition(Output, default, args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Storage);

        commandBuffer.BeginExternalCommands();

        scaler.ColorTexture = args.Color.Metal();
        scaler.DepthTexture = args.Depth.Metal();
        scaler.MotionTexture = args.MotionVectors.Metal();
        scaler.DiffuseAlbedoTexture = args.DiffuseAlbedo.Metal();
        scaler.SpecularAlbedoTexture = args.SpecularAlbedo.Metal();
        scaler.NormalTexture = args.Normals.Metal();
        scaler.RoughnessTexture = args.Roughness.Metal();
        scaler.SpecularHitDistanceTexture = args.SpecularHitDistance?.Metal() ?? default;
        scaler.TransparencyOverlayTexture = args.TransparencyOverlay?.Metal() ?? default;
        scaler.OutputTexture = Output.Metal();
        scaler.ExposureTexture = args.Exposure?.Metal() ?? default;
        scaler.PreExposure = args.PreExposure ?? 1.0f;
        scaler.ReactiveMaskTexture = args.ReactiveMask?.Metal() ?? default;
        scaler.JitterOffsetX = -args.JitterOffset.X;
        scaler.JitterOffsetY = -args.JitterOffset.Y;
        scaler.MotionVectorScaleX = args.MotionVectors.Desc.Width;
        scaler.MotionVectorScaleY = args.MotionVectors.Desc.Height;
        scaler.ShouldResetHistory = args.Reset;
        scaler.IsDepthReversed = UpscalingHelper.IsDepthReversed(args.Projection);
        scaler.WorldToViewMatrix = MetalFXFormats.Metal(args.View);
        scaler.ViewToClipMatrix = MetalFXFormats.Metal(args.Projection);
        scaler.Encode(commandBuffer.Metal());

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(Output, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        scaler.Dispose();

        base.Destroy();
    }
}
