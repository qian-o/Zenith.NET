using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Passes;

internal class UpscalingPass : DisposableObject
{
    private readonly TemporalUpscaler? temporalUpscaler;
    private readonly DenoisingUpscaler? denoisingUpscaler;

    public UpscalingPass(UpscalingMode mode, bool isDenoisingEnabled, uint width, uint height)
    {
        if (isDenoisingEnabled)
        {
            InputSize = App.Context.GetDenoisingUpscalerInputSize(width, height, mode);
            denoisingUpscaler = App.Context.CreateDenoisingUpscaler(new()
            {
                ColorFormat = PixelFormat.R16G16B16A16Float,
                DepthFormat = PixelFormat.R32Float,
                MotionVectorsFormat = PixelFormat.R32G32Float,
                DiffuseAlbedoFormat = PixelFormat.R16G16B16A16Float,
                SpecularAlbedoFormat = PixelFormat.R16G16B16A16Float,
                NormalsFormat = PixelFormat.R16G16B16A16Float,
                RoughnessFormat = PixelFormat.R16Float,
                SpecularHitDistanceFormat = PixelFormat.R32Float,
                OutputFormat = PixelFormat.R16G16B16A16Float,
                InputWidth = InputSize.Width,
                InputHeight = InputSize.Height,
                OutputWidth = width,
                OutputHeight = height
            });
        }
        else
        {
            InputSize = App.Context.GetTemporalUpscalerInputSize(width, height, mode);
            temporalUpscaler = App.Context.CreateTemporalUpscaler(new()
            {
                ColorFormat = PixelFormat.R16G16B16A16Float,
                DepthFormat = PixelFormat.R32Float,
                MotionVectorsFormat = PixelFormat.R32G32Float,
                OutputFormat = PixelFormat.R16G16B16A16Float,
                InputWidth = InputSize.Width,
                InputHeight = InputSize.Height,
                OutputWidth = width,
                OutputHeight = height,
                Flags = UpscalingInputFlags.AutoExposure
            });
        }

        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment
        });
    }

    public UpscalingInputSize InputSize { get; }

    public Texture Color { get; }

    public void Record(CommandBuffer commandBuffer, PathTracingOutput input, CameraHandler camera, Vector2 jitterOffset, bool reset)
    {
        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.Storage);

        temporalUpscaler?.Dispatch(commandBuffer, new()
        {
            Color = input.Color,
            Depth = input.Depth,
            MotionVectors = input.MotionVectors,
            Output = Color,
            JitterOffset = -jitterOffset,
            MotionVectorScale = Vector2.One,
            PreExposure = 1.0f,
            Reset = reset
        });

        denoisingUpscaler?.Dispatch(commandBuffer, new()
        {
            Color = input.Color,
            Depth = input.Depth,
            MotionVectors = input.MotionVectors,
            DiffuseAlbedo = input.DiffuseAlbedo,
            SpecularAlbedo = input.SpecularAlbedo,
            Normals = input.Normals,
            Roughness = input.Roughness,
            SpecularHitDistance = input.SpecularHitDistance,
            Output = Color,
            JitterOffset = -jitterOffset,
            MotionVectorScale = Vector2.One,
            WorldToView = camera.View,
            ViewToClip = camera.Projection,
            PreExposure = 1.0f,
            Reset = reset
        });

        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        Color?.Dispose();
        denoisingUpscaler?.Dispose();
        temporalUpscaler?.Dispose();
    }
}
