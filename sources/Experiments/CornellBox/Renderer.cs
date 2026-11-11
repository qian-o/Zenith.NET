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

    public Renderer(RenderSettings settings)
    {
        Settings = settings;

        scene = new();
        pathTracingPass = new();
        outputPass = new();

        Resize();
    }

    public RenderSettings Settings { get; private set; }

    public void ApplySettings(RenderSettings settings)
    {
        bool hasRenderChanges = Settings.UpscalingQuality != settings.UpscalingQuality || Settings.IsRayReconstructionEnabled != settings.IsRayReconstructionEnabled;

        Settings = settings;

        if (hasRenderChanges)
        {
            Resize();
        }
    }

    public void Update(double deltaSeconds)
    {
        if (!Settings.IsPaused)
        {
            elapsedSeconds += deltaSeconds;
        }
    }

    public RenderedFrame Render(CommandBuffer commandBuffer, CameraHandler camera, double deltaSeconds)
    {
        uint sampleIndex = frameIndex + 1u;
        Vector2 jitterOffsetInPixels = upscalingPass is null ? Vector2.Zero : new(GetHaltonSample(sampleIndex, 2u) - 0.5f, GetHaltonSample(sampleIndex, 3u) - 0.5f);
        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        float deltaTimeInSeconds = (float)deltaSeconds;
        bool reset = frameIndex is 0;

        scene.Update(commandBuffer, elapsedSeconds);
        pathTracingPass.Record(commandBuffer, scene, camera, jitterOffsetInPixels, frameIndex, upscalingPass?.IsRayReconstructionEnabled is true);

        PathTracingOutput output = pathTracingPass.Output;
        Texture color = output.Color;

        if (upscalingPass is not null)
        {
            upscalingPass.Record(commandBuffer, output, jitterOffsetInPixels, view, projection, deltaTimeInSeconds, reset);
            color = upscalingPass.Color;
        }

        outputPass.Record(commandBuffer, color, frameIndex);

        RenderedFrame frame = new()
        {
            Color = outputPass.Color,
            Depth = output.Depth,
            MotionVectors = output.MotionVectors,
            JitterOffset = jitterOffsetInPixels,
            View = view,
            Projection = projection,
            DeltaTimeInSeconds = deltaTimeInSeconds,
            Reset = reset
        };

        frameIndex++;

        return frame;
    }

    public void Resize()
    {
        if (Settings.UpscalingQuality is UpscalingQuality quality)
        {
            if (upscalingPass is null || upscalingPass.Quality != quality || upscalingPass.IsRayReconstructionEnabled != Settings.IsRayReconstructionEnabled)
            {
                upscalingPass?.Dispose();
                upscalingPass = null;

                upscalingPass = new(quality, Settings.IsRayReconstructionEnabled);
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

        uint renderWidth = upscalingPass?.RenderWidth ?? App.PhysicalWidth;
        uint renderHeight = upscalingPass?.RenderHeight ?? App.PhysicalHeight;

        pathTracingPass.Resize(renderWidth, renderHeight);
        outputPass.Resize();

        frameIndex = 0;
    }

    protected override void Destroy()
    {
        upscalingPass?.Dispose();
        outputPass.Dispose();
        pathTracingPass.Dispose();
        scene.Dispose();
    }

    private static float GetHaltonSample(uint index, uint radix)
    {
        float value = 0.0f;
        float weight = 1.0f;

        while (index > 0u)
        {
            weight /= radix;
            value += weight * (index % radix);
            index /= radix;
        }

        return value;
    }
}
