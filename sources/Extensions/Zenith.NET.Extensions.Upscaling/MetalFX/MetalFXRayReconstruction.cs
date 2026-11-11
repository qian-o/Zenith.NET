using System.Numerics;
using System.Runtime.CompilerServices;
using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXRayReconstruction(MetalFXVendor vendor, RayReconstructionDesc desc) : RayReconstruction(desc)
{
    private MTL4FXTemporalDenoisedScaler scaler;
    private (PixelFormat Color,
             PixelFormat Depth,
             PixelFormat MotionVectors,
             PixelFormat DiffuseAlbedo,
             PixelFormat SpecularAlbedo,
             PixelFormat Normals,
             PixelFormat Roughness,
             PixelFormat SpecularHitDistance,
             PixelFormat? ReactiveMask,
             PixelFormat? TransparencyOverlay,
             PixelFormat Output,
             bool AutoExposure) key;
    private bool recreated;

    protected override void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        using NSAutoreleasePool pool = new();

        (_, _, _, _, bool depthReversed, _) = Vendor.DecomposePerspective(args.Projection);

        EnsureScaler((args.Color.Desc.Format,
                      args.Depth.Desc.Format,
                      args.MotionVectors.Desc.Format,
                      args.DiffuseAlbedo.Desc.Format,
                      args.SpecularAlbedo.Desc.Format,
                      args.Normals.Desc.Format,
                      args.Roughness.Desc.Format,
                      args.SpecularHitDistance.Desc.Format,
                      args.ReactiveMask?.Desc.Format,
                      args.TransparencyOverlay?.Desc.Format,
                      args.Output.Desc.Format,
                      args.Exposure is null),
                     depthReversed);

        if (recreated)
        {
            args.Reset = true;
            recreated = false;
        }

        commandBuffer.BeginExternalCommands();

        Encode(commandBuffer.Metal(), args, depthReversed);

        commandBuffer.EndExternalCommands();
    }

    protected override void Destroy()
    {
        if (!scaler.IsNull)
        {
            scaler.Dispose();
        }
    }

    private void EnsureScaler((PixelFormat Color,
                               PixelFormat Depth,
                               PixelFormat MotionVectors,
                               PixelFormat DiffuseAlbedo,
                               PixelFormat SpecularAlbedo,
                               PixelFormat Normals,
                               PixelFormat Roughness,
                               PixelFormat SpecularHitDistance,
                               PixelFormat? ReactiveMask,
                               PixelFormat? TransparencyOverlay,
                               PixelFormat Output,
                               bool AutoExposure) nextKey,
                              bool depthReversed)
    {
        if (!scaler.IsNull && key == nextKey)
        {
            return;
        }

        if (!scaler.IsNull)
        {
            vendor.Context.GraphicsQueue.Timeline.Signal().Wait();
            scaler.Dispose();
        }

        key = nextKey;

        using MTLFXTemporalDenoisedScalerDescriptor descriptor = new()
        {
            ColorTextureFormat = MetalFXFormats.Metal(nextKey.Color),
            DepthTextureFormat = MetalFXFormats.Metal(nextKey.Depth),
            MotionTextureFormat = MetalFXFormats.Metal(nextKey.MotionVectors),
            DiffuseAlbedoTextureFormat = MetalFXFormats.Metal(nextKey.DiffuseAlbedo),
            SpecularAlbedoTextureFormat = MetalFXFormats.Metal(nextKey.SpecularAlbedo),
            NormalTextureFormat = MetalFXFormats.Metal(nextKey.Normals),
            RoughnessTextureFormat = MetalFXFormats.Metal(nextKey.Roughness),
            SpecularHitDistanceTextureFormat = MetalFXFormats.Metal(nextKey.SpecularHitDistance),
            TransparencyOverlayTextureFormat = MetalFXFormats.Metal(nextKey.TransparencyOverlay),
            OutputTextureFormat = MetalFXFormats.Metal(nextKey.Output),
            InputWidth = Desc.RenderWidth,
            InputHeight = Desc.RenderHeight,
            OutputWidth = Desc.OutputWidth,
            OutputHeight = Desc.OutputHeight,
            IsAutoExposureEnabled = nextKey.AutoExposure,
            IsReactiveMaskTextureEnabled = nextKey.ReactiveMask is not null,
            ReactiveMaskTextureFormat = MetalFXFormats.Metal(nextKey.ReactiveMask),
            IsSpecularHitDistanceTextureEnabled = true,
            IsTransparencyOverlayTextureEnabled = nextKey.TransparencyOverlay is not null
        };
        scaler = descriptor.MakeTemporalDenoisedScaler(vendor.Device, vendor.Compiler);

        WarmUp(nextKey, depthReversed);
        recreated = true;
    }

    private void Encode(MTL4CommandBuffer commandBuffer, RayReconstructionArgs args, bool depthReversed)
    {
        scaler.ColorTexture = args.Color.Metal();
        scaler.DepthTexture = args.Depth.Metal();
        scaler.MotionTexture = args.MotionVectors.Metal();
        scaler.DiffuseAlbedoTexture = args.DiffuseAlbedo.Metal();
        scaler.SpecularAlbedoTexture = args.SpecularAlbedo.Metal();
        scaler.NormalTexture = args.Normals.Metal();
        scaler.RoughnessTexture = args.Roughness.Metal();
        scaler.SpecularHitDistanceTexture = args.SpecularHitDistance.Metal();
        scaler.ExposureTexture = args.Exposure?.Metal() ?? default;
        scaler.ReactiveMaskTexture = args.ReactiveMask?.Metal() ?? default;
        scaler.TransparencyOverlayTexture = args.TransparencyOverlay?.Metal() ?? default;
        scaler.OutputTexture = args.Output.Metal();
        scaler.JitterOffsetX = -args.JitterOffset.X;
        scaler.JitterOffsetY = -args.JitterOffset.Y;
        scaler.MotionVectorScaleX = Desc.RenderWidth;
        scaler.MotionVectorScaleY = Desc.RenderHeight;
        scaler.WorldToViewMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.View);
        scaler.ViewToClipMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.Projection);
        scaler.PreExposure = args.PreExposure ?? 1.0f;
        scaler.ShouldResetHistory = args.Reset;
        scaler.IsDepthReversed = depthReversed;
        scaler.Encode(commandBuffer);
    }

    private void WarmUp((PixelFormat Color,
                         PixelFormat Depth,
                         PixelFormat MotionVectors,
                         PixelFormat DiffuseAlbedo,
                         PixelFormat SpecularAlbedo,
                         PixelFormat Normals,
                         PixelFormat Roughness,
                         PixelFormat SpecularHitDistance,
                         PixelFormat? ReactiveMask,
                         PixelFormat? TransparencyOverlay,
                         PixelFormat Output,
                         bool AutoExposure) nextKey,
                        bool depthReversed)
    {
        using Texture color = vendor.CreateTexture(nextKey.Color, Desc.RenderWidth, Desc.RenderHeight);
        using Texture depth = vendor.CreateTexture(nextKey.Depth, Desc.RenderWidth, Desc.RenderHeight);
        using Texture motionVectors = vendor.CreateTexture(nextKey.MotionVectors, Desc.RenderWidth, Desc.RenderHeight);
        using Texture diffuseAlbedo = vendor.CreateTexture(nextKey.DiffuseAlbedo, Desc.RenderWidth, Desc.RenderHeight);
        using Texture specularAlbedo = vendor.CreateTexture(nextKey.SpecularAlbedo, Desc.RenderWidth, Desc.RenderHeight);
        using Texture normals = vendor.CreateTexture(nextKey.Normals, Desc.RenderWidth, Desc.RenderHeight);
        using Texture roughness = vendor.CreateTexture(nextKey.Roughness, Desc.RenderWidth, Desc.RenderHeight);
        using Texture specularHitDistance = vendor.CreateTexture(nextKey.SpecularHitDistance, Desc.RenderWidth, Desc.RenderHeight);
        using Texture? reactiveMask = nextKey.ReactiveMask is PixelFormat reactiveMaskFormat ? vendor.CreateTexture(reactiveMaskFormat, Desc.RenderWidth, Desc.RenderHeight) : null;
        using Texture? transparencyOverlay = nextKey.TransparencyOverlay is PixelFormat transparencyOverlayFormat ? vendor.CreateTexture(transparencyOverlayFormat, Desc.RenderWidth, Desc.RenderHeight) : null;
        using Texture? exposure = nextKey.AutoExposure ? null : vendor.CreateTexture(PixelFormat.R16Float, 1, 1);
        using Texture output = vendor.CreateTexture(nextKey.Output, Desc.OutputWidth, Desc.OutputHeight);

        vendor.WarmUp(commandBuffer => Encode(commandBuffer, new()
        {
            Color = color,
            Depth = depth,
            MotionVectors = motionVectors,
            DiffuseAlbedo = diffuseAlbedo,
            SpecularAlbedo = specularAlbedo,
            Normals = normals,
            Roughness = roughness,
            SpecularHitDistance = specularHitDistance,
            ReactiveMask = reactiveMask,
            TransparencyOverlay = transparencyOverlay,
            Exposure = exposure,
            Output = output,
            View = Matrix4x4.Identity,
            Projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4.0f, (float)Desc.OutputWidth / Desc.OutputHeight, 0.1f, 100.0f),
            PreExposure = 1.0f,
            Reset = true
        }, depthReversed));
    }
}
