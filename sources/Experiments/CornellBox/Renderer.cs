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
    private FrameGenerationPass? frameGenerationPass;
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
    } = App.Context.UpscalingCapabilities.TemporalUpscalerSupported ? ZenithUpscalingMode.Quality : null;

    public bool IsDenoisingEnabled
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
    } = App.Context.UpscalingCapabilities.DenoisingUpscalerSupported;

    public bool IsFrameGenerationEnabled
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
    } = App.Context.UpscalingCapabilities.FrameGeneratorSupported;

    public Texture Overlay => outputPass.Overlay;

    public ReadOnlySpan<Texture> Render(CommandBuffer commandBuffer, CameraHandler camera, double deltaSeconds)
    {
        if (!IsPaused)
        {
            elapsedSeconds += deltaSeconds;
        }

        uint sampleIndex = frameIndex + 1u;
        Vector2 jitterOffsetInPixels = upscalingPass is null ? Vector2.Zero : new(GetHaltonSample(sampleIndex, 2u) - 0.5f, GetHaltonSample(sampleIndex, 3u) - 0.5f);
        bool reset = frameIndex is 0;

        scene.Update(commandBuffer, elapsedSeconds);
        pathTracingPass.Record(commandBuffer, scene, camera, jitterOffsetInPixels, frameIndex);

        Texture color = pathTracingPass.Output.Color;

        if (upscalingPass is not null)
        {
            upscalingPass.Record(commandBuffer, pathTracingPass.Output, camera, jitterOffsetInPixels, reset);
            color = upscalingPass.Color;
        }

        outputPass.Record(commandBuffer, color, frameIndex);
        frameGenerationPass?.Record(commandBuffer, outputPass.Color, pathTracingPass.Output, camera, jitterOffsetInPixels, deltaSeconds, reset);

        frameIndex++;

        if (frameGenerationPass is null || reset)
        {
            return outputPass.Composite(commandBuffer, [outputPass.Color]);
        }

        return outputPass.Composite(commandBuffer, [frameGenerationPass.Color, outputPass.Color]);
    }

    public void Resize()
    {
        upscalingPass?.Dispose();
        upscalingPass = null;

        if (UpscalingMode is ZenithUpscalingMode mode)
        {
            upscalingPass = new(mode, IsDenoisingEnabled);
        }

        UpscalingInputSize inputSize = upscalingPass?.InputSize ?? new(App.PhysicalWidth, App.PhysicalHeight);

        pathTracingPass.Resize(inputSize.Width, inputSize.Height);
        outputPass.Resize(IsFrameGenerationEnabled ? 2 : 1);

        frameGenerationPass?.Dispose();
        frameGenerationPass = null;

        if (IsFrameGenerationEnabled)
        {
            frameGenerationPass = new(inputSize.Width, inputSize.Height);
        }

        frameIndex = 0;
    }

    protected override void Destroy()
    {
        frameGenerationPass?.Dispose();
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
