using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXRayReconstruction(MetalFXVendor vendor, RayReconstructionDesc desc) : RayReconstruction(vendor.Context, desc)
{
    private Key key;
    private MTL4FXTemporalDenoisedScaler scaler;

    protected override void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        using NSAutoreleasePool _ = new();

        if (Ensure(args))
        {
            args.Reset = true;
        }

        commandBuffer.Transition(Output, default, TextureLayout.Undefined, TextureLayout.Storage);

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
        scaler.MotionVectorScaleX = Desc.RenderWidth;
        scaler.MotionVectorScaleY = Desc.RenderHeight;
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

    private bool Ensure(RayReconstructionArgs args)
    {
        Key newKey = new(args);

        if (key.Equals(newKey))
        {
            return false;
        }

        key = newKey;

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

        return true;
    }

    private readonly struct Key(RayReconstructionArgs args)
    {
        public readonly PixelFormat Color = args.Color.Desc.Format;

        public readonly PixelFormat Depth = args.Depth.Desc.Format;

        public readonly PixelFormat MotionVectors = args.MotionVectors.Desc.Format;

        public readonly PixelFormat DiffuseAlbedo = args.DiffuseAlbedo.Desc.Format;

        public readonly PixelFormat SpecularAlbedo = args.SpecularAlbedo.Desc.Format;

        public readonly PixelFormat Normals = args.Normals.Desc.Format;

        public readonly PixelFormat Roughness = args.Roughness.Desc.Format;

        public readonly PixelFormat SpecularHitDistance = args.SpecularHitDistance?.Desc.Format ?? default;

        public readonly PixelFormat ReactiveMask = args.ReactiveMask?.Desc.Format ?? default;

        public readonly PixelFormat TransparencyOverlay = args.TransparencyOverlay?.Desc.Format ?? default;

        public readonly PixelFormat Exposure = args.Exposure?.Desc.Format ?? default;

        public bool Equals(Key other)
        {
            return Color == other.Color && Depth == other.Depth && MotionVectors == other.MotionVectors && DiffuseAlbedo == other.DiffuseAlbedo && SpecularAlbedo == other.SpecularAlbedo && Normals == other.Normals && Roughness == other.Roughness && SpecularHitDistance == other.SpecularHitDistance && ReactiveMask == other.ReactiveMask && TransparencyOverlay == other.TransparencyOverlay && Exposure == other.Exposure;
        }
    }
}
