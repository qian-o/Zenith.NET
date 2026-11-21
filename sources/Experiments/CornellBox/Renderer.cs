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
    private bool wasPaused;

    public Renderer()
    {
        scene = new();
        pathTracingPass = new();
        outputPass = new();

        Resize();
    }

    public UpscalingQuality? Quality { get; set; } = App.Context.UpscalingCapabilities.SuperResolutionSupported ? UpscalingQuality.Quality : null;

    public bool IsRayReconstructionEnabled { get; set; } = App.Context.UpscalingCapabilities.RayReconstructionSupported;

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
        wasPaused = false;
    }

    public FrameGenerationSwapChainArgs Render(CommandBuffer commandBuffer, CameraHandler camera, double deltaSeconds)
    {
        bool reset = frameIndex is 0;
        Vector2 jitterOffset = upscalingPass is null ? Vector2.Zero : UpscalingHelper.JitterOffset(frameIndex, upscalingPass.RenderWidth, upscalingPass.RenderHeight, App.PhysicalWidth, App.PhysicalHeight);

        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        float deltaTimeInSeconds = (float)Math.Clamp(deltaSeconds, 1.0 / 500.0, 0.25);

        if (IsPaused && !reset)
        {
            if (!wasPaused)
            {
                scene.FreezeMotion(commandBuffer);
            }
        }
        else
        {
            scene.Update(commandBuffer, elapsedSeconds, reset);
        }

        wasPaused = IsPaused;

        pathTracingPass.Record(commandBuffer, scene, camera, view, projection, jitterOffset, frameIndex, reset);

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
        UpscalingQuality? effectiveQuality = App.Context.UpscalingCapabilities.SuperResolutionSupported ? Quality : null;
        bool effectiveRayReconstruction = effectiveQuality is not null && IsRayReconstructionEnabled && App.Context.UpscalingCapabilities.RayReconstructionSupported;

        if (effectiveQuality is UpscalingQuality activeQuality)
        {
            if (upscalingPass is null || upscalingPass.Quality != activeQuality || upscalingPass.IsRayReconstructionEnabled != effectiveRayReconstruction)
            {
                upscalingPass?.Dispose();
                upscalingPass = new(activeQuality, effectiveRayReconstruction);
            }
            else if (upscalingPass.Color.Desc.Width != App.PhysicalWidth || upscalingPass.Color.Desc.Height != App.PhysicalHeight)
            {
                upscalingPass.Resize();
            }
        }
        else
        {
            upscalingPass?.Dispose();
            upscalingPass = null;
        }

        pathTracingPass.Resize(upscalingPass?.RenderWidth ?? App.PhysicalWidth, upscalingPass?.RenderHeight ?? App.PhysicalHeight, upscalingPass is not null, effectiveRayReconstruction);
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
