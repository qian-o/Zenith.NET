using System.Numerics;
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

    public void Record(CommandBuffer commandBuffer,
                       PathTracingPass pathTracing,
                       Vector2 jitterOffset,
                       Matrix4x4 view,
                       Matrix4x4 projection,
                       float deltaTimeInSeconds,
                       bool reset)
    {
        superResolution?.Dispatch(commandBuffer, new()
        {
            Color = pathTracing.Color,
            Depth = pathTracing.Depth,
            MotionVectors = pathTracing.MotionVectors,
            ReactiveMask = pathTracing.ReactiveMask,
            JitterOffset = jitterOffset,
            Projection = projection,
            DeltaTimeInSeconds = deltaTimeInSeconds,
            Reset = reset
        });

        rayReconstruction?.Dispatch(commandBuffer, new()
        {
            Color = pathTracing.Color,
            Depth = pathTracing.Depth,
            MotionVectors = pathTracing.MotionVectors,
            DiffuseAlbedo = pathTracing.DiffuseAlbedo!,
            SpecularAlbedo = pathTracing.SpecularAlbedo!,
            Normals = pathTracing.Normals!,
            Roughness = pathTracing.Roughness!,
            SpecularHitDistance = pathTracing.SpecularHitDistance,
            ReactiveMask = pathTracing.ReactiveMask,
            JitterOffset = jitterOffset,
            View = view,
            Projection = projection,
            DeltaTimeInSeconds = deltaTimeInSeconds,
            Reset = reset
        });
    }

    public void Resize()
    {
        if (Color.Desc.Width == App.PhysicalWidth && Color.Desc.Height == App.PhysicalHeight)
        {
            return;
        }

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
            rayReconstruction = App.Context.CreateRayReconstruction(RayReconstructionDesc.Preset(PathTracingPass.ColorFormat, App.PhysicalWidth, App.PhysicalHeight, Quality));
        }
        else
        {
            superResolution = App.Context.CreateSuperResolution(SuperResolutionDesc.Preset(PathTracingPass.ColorFormat, App.PhysicalWidth, App.PhysicalHeight, Quality));
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
