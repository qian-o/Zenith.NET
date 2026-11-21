using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using CornellBox.Passes;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox;

internal class Renderer : DisposableObject
{
    private readonly SceneResources scene;
    private readonly PathTracingPass pathTracingPass;
    private readonly OutputPass outputPass;

    private UpscalingPass? upscalingPass;
    private double elapsedSeconds;
    private uint frameIndex;

    public Renderer()
    {
        scene = new();
        pathTracingPass = new();
        outputPass = new();

        Resize();
    }

    public UpscalingQuality? Quality { get; set; } = App.Context.UpscalingCapabilities.SuperResolutionSupported ? UpscalingQuality.Quality : null;

    public bool IsRayReconstructionEnabled { get; set; } = App.Context.UpscalingCapabilities.SuperResolutionSupported && App.Context.UpscalingCapabilities.RayReconstructionSupported;

    public bool IsPaused { get; set; }

    public Texture Color => outputPass.Color;

    public void Update(double deltaSeconds)
    {
        if (!IsPaused)
        {
            elapsedSeconds += deltaSeconds;
        }
    }

    public void Reset()
    {
        IsPaused = false;
        elapsedSeconds = 0.0;
        frameIndex = 0;
    }

    public FrameGenerationSwapChainArgs Render(CommandBuffer commandBuffer, CameraHandler camera, double deltaSeconds)
    {
        bool reset = frameIndex is 0;
        Vector2 jitterOffset = upscalingPass is null ? Vector2.Zero : UpscalingHelper.JitterOffset(frameIndex, upscalingPass.RenderWidth, upscalingPass.RenderHeight, App.PhysicalWidth, App.PhysicalHeight);

        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        float deltaTimeInSeconds = (float)deltaSeconds;

        scene.Update(commandBuffer, elapsedSeconds, IsPaused, reset);

        pathTracingPass.Record(commandBuffer, scene, camera.Position, view, projection, jitterOffset, frameIndex, reset);

        Texture color = pathTracingPass.Color;

        if (upscalingPass is not null)
        {
            upscalingPass.Record(commandBuffer, pathTracingPass, jitterOffset, view, projection, deltaTimeInSeconds, reset);
            color = upscalingPass.Color;
        }

        outputPass.Record(commandBuffer, color, frameIndex);

        frameIndex++;

        return new()
        {
            Depth = pathTracingPass.Depth,
            MotionVectors = pathTracingPass.MotionVectors,
            Hudless = outputPass.Color,
            JitterOffset = jitterOffset,
            View = view,
            Projection = projection,
            DeltaTimeInSeconds = deltaTimeInSeconds,
            Reset = reset
        };
    }

    public void Resize()
    {
        if (Quality is UpscalingQuality quality)
        {
            if (upscalingPass is null || upscalingPass.Quality != quality || upscalingPass.IsRayReconstructionEnabled != IsRayReconstructionEnabled)
            {
                upscalingPass?.Dispose();
                upscalingPass = new(quality, IsRayReconstructionEnabled);
            }
            else
            {
                upscalingPass.Resize();
            }
        }
        else
        {
            upscalingPass?.Dispose();
            upscalingPass = null;
        }

        pathTracingPass.Resize(upscalingPass?.RenderWidth ?? App.PhysicalWidth, upscalingPass?.RenderHeight ?? App.PhysicalHeight, upscalingPass is not null, upscalingPass?.IsRayReconstructionEnabled is true);
        outputPass.Resize();

        frameIndex = 0;
    }

    protected override void Destroy()
    {
        upscalingPass?.Dispose();
        outputPass?.Dispose();
        pathTracingPass?.Dispose();
        scene?.Dispose();
    }
}
