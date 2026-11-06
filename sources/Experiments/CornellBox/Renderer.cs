using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using CornellBox.Passes;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;
using ZenithUpscalingMode = Zenith.NET.Extensions.Upscaling.UpscalingMode;

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

    public bool IsPaused { get; set; }

    public ZenithUpscalingMode? UpscalingMode
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                Resize();
            }
        }
    } = App.Context.UpscalingCapabilities.SuperResolutionSupported ? ZenithUpscalingMode.Quality : null;

    public bool IsRayReconstructionEnabled
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                Resize();
            }
        }
    } = App.Context.UpscalingCapabilities.RayReconstructionSupported;

    public Texture Color => outputPass.Color;

    public void Update(double deltaSeconds)
    {
        if (!IsPaused)
        {
            elapsedSeconds += deltaSeconds;
        }
    }

    public void Render(CommandBuffer commandBuffer, CameraHandler camera, double deltaSeconds)
    {
        uint sampleIndex = frameIndex + 1u;
        Vector2 jitterOffsetInPixels = upscalingPass is null ? Vector2.Zero : new(GetHaltonSample(sampleIndex, 2u) - 0.5f, GetHaltonSample(sampleIndex, 3u) - 0.5f);
        bool reset = frameIndex is 0;

        scene.Update(commandBuffer, elapsedSeconds);
        pathTracingPass.Record(commandBuffer, scene, camera, jitterOffsetInPixels, frameIndex);

        Texture color = pathTracingPass.Output.Color;

        if (upscalingPass is not null)
        {
            upscalingPass.Record(commandBuffer, pathTracingPass.Output, camera, jitterOffsetInPixels, (float)deltaSeconds, reset);
            color = upscalingPass.Color;
        }

        outputPass.Record(commandBuffer, color, frameIndex);

        frameIndex++;
    }

    public void Resize()
    {
        if (UpscalingMode is ZenithUpscalingMode mode)
        {
            if (upscalingPass is null || upscalingPass.Mode != mode || upscalingPass.IsRayReconstructionEnabled != IsRayReconstructionEnabled)
            {
                upscalingPass?.Dispose();
                upscalingPass = new(mode, IsRayReconstructionEnabled);
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

        UpscalingInputSize inputSize = upscalingPass?.InputSize ?? new(App.PhysicalWidth, App.PhysicalHeight);

        pathTracingPass.Resize(inputSize.Width, inputSize.Height);
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
