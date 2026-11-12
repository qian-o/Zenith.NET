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

    public Texture Color => rayReconstruction?.Output ?? superResolution!.Output;

    public void Record(CommandBuffer commandBuffer, PathTracingOutput input, Vector2 jitterOffset, Matrix4x4 view, Matrix4x4 projection, float deltaTimeInSeconds, bool reset)
    {
        superResolution?.Dispatch(commandBuffer, new()
        {
            Color = input.Color,
            Depth = input.Depth,
            MotionVectors = input.MotionVectors,
            ReactiveMask = input.ReactiveMask,
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
            JitterOffset = jitterOffset,
            View = view,
            Projection = projection,
            DeltaTimeInSeconds = deltaTimeInSeconds,
            Reset = reset
        });
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
            rayReconstruction = App.Context.CreateRayReconstruction(RayReconstructionDesc.Quality(App.Context, PathTracingOutput.ColorFormat, App.PhysicalWidth, App.PhysicalHeight, Quality));
        }
        else
        {
            superResolution = App.Context.CreateSuperResolution(SuperResolutionDesc.Quality(App.Context, PathTracingOutput.ColorFormat, App.PhysicalWidth, App.PhysicalHeight, Quality));
        }
    }

    private void DestroyResources()
    {
        rayReconstruction?.Dispose();
        rayReconstruction = null;
        superResolution?.Dispose();
        superResolution = null;
    }
}
