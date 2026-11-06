using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Passes;

internal class UpscalingPass : DisposableObject
{
    private SuperResolution? superResolution;
    private RayReconstruction? rayReconstruction;

    public UpscalingPass(UpscalingMode mode, bool isRayReconstructionEnabled)
    {
        Mode = mode;
        IsRayReconstructionEnabled = isRayReconstructionEnabled;

        CreateResources();
    }

    public UpscalingMode Mode { get; }

    public bool IsRayReconstructionEnabled { get; }

    public UpscalingInputSize InputSize { get; private set; }

    public Texture Color { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, PathTracingOutput input, CameraHandler camera, Vector2 jitterOffsetInPixels, float deltaTimeInSeconds, bool reset)
    {
        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.Storage);

        superResolution?.Dispatch(commandBuffer, new()
        {
            Color = input.Color,
            Depth = input.Depth,
            MotionVectors = input.MotionVectors,
            Output = Color,
            JitterOffset = -jitterOffsetInPixels,
            WorldToView = camera.View,
            ViewToClip = camera.Projection,
            DeltaTimeInSeconds = deltaTimeInSeconds,
            PreExposure = 1.0f,
            Reset = reset
        });

        rayReconstruction?.Dispatch(commandBuffer, new()
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
            JitterOffset = -jitterOffsetInPixels,
            WorldToView = camera.View,
            ViewToClip = camera.Projection,
            DeltaTimeInSeconds = deltaTimeInSeconds,
            PreExposure = 1.0f,
            Reset = reset
        });

        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    public void Resize()
    {
        DestroyResources();
        CreateResources();
    }

    protected override void Destroy()
    {
        DestroyResources();
    }

    private void CreateResources()
    {
        if (IsRayReconstructionEnabled)
        {
            InputSize = App.Context.GetRayReconstructionInputSize(App.PhysicalWidth, App.PhysicalHeight, Mode);

            rayReconstruction = App.Context.CreateRayReconstruction(new()
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
                IsHdr = true,
                InputWidth = InputSize.Width,
                InputHeight = InputSize.Height,
                OutputWidth = App.PhysicalWidth,
                OutputHeight = App.PhysicalHeight,
                Flags = UpscalingInputFlags.AutoExposure
            });
        }
        else
        {
            InputSize = App.Context.GetSuperResolutionInputSize(App.PhysicalWidth, App.PhysicalHeight, Mode);

            superResolution = App.Context.CreateSuperResolution(new()
            {
                ColorFormat = PixelFormat.R16G16B16A16Float,
                DepthFormat = PixelFormat.R32Float,
                MotionVectorsFormat = PixelFormat.R32G32Float,
                OutputFormat = PixelFormat.R16G16B16A16Float,
                IsHdr = true,
                InputWidth = InputSize.Width,
                InputHeight = InputSize.Height,
                OutputWidth = App.PhysicalWidth,
                OutputHeight = App.PhysicalHeight,
                Flags = UpscalingInputFlags.AutoExposure
            });
        }

        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, App.PhysicalWidth, App.PhysicalHeight, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment
        });
    }

    private void DestroyResources()
    {
        Color?.Dispose();
        Color = null!;
        rayReconstruction?.Dispose();
        rayReconstruction = null;
        superResolution?.Dispose();
        superResolution = null;
    }
}
