using System.Numerics;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Passes;

internal class UpscalingPass : DisposableObject
{
    private SuperResolution? superResolution;
    private RayReconstruction? rayReconstruction;

    public UpscalingPass(UpscalingQuality quality, bool isRayReconstructionEnabled)
    {
        Quality = quality;
        IsRayReconstructionEnabled = isRayReconstructionEnabled;

        CreateResources();
    }

    public UpscalingQuality Quality { get; }

    public bool IsRayReconstructionEnabled { get; }

    public uint RenderWidth => rayReconstruction?.Desc.RenderWidth ?? superResolution!.Desc.RenderWidth;

    public uint RenderHeight => rayReconstruction?.Desc.RenderHeight ?? superResolution!.Desc.RenderHeight;

    public Texture Color { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, PathTracingOutput input, Vector2 jitterOffset, Matrix4x4 view, Matrix4x4 projection, float deltaTimeInSeconds, bool reset)
    {
        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.Storage);

        superResolution?.Dispatch(commandBuffer, new()
        {
            Color = input.Color,
            Depth = input.Depth,
            MotionVectors = input.MotionVectors,
            ReactiveMask = input.ReactiveMask,
            Output = Color,
            JitterOffset = jitterOffset,
            Projection = projection,
            DeltaTimeInSeconds = deltaTimeInSeconds,
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
            ReactiveMask = input.ReactiveMask,
            Output = Color,
            JitterOffset = jitterOffset,
            View = view,
            Projection = projection,
            DeltaTimeInSeconds = deltaTimeInSeconds,
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
            rayReconstruction = App.Context.CreateRayReconstruction(RayReconstructionDesc.Quality(App.Context, App.PhysicalWidth, App.PhysicalHeight, Quality));
        }
        else
        {
            superResolution = App.Context.CreateSuperResolution(SuperResolutionDesc.Quality(App.Context, App.PhysicalWidth, App.PhysicalHeight, Quality));
        }

        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PathTracingOutput.ColorFormat, App.PhysicalWidth, App.PhysicalHeight, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment | TextureUsages.TransferDst
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
