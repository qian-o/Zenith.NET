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
    private FrameGenerationPass? frameGenerationPass;
    private double elapsedSeconds;
    private uint frameIndex;

    public Renderer(uint width, uint height)
    {
        scene = new();
        pathTracingPass = new();
        outputPass = new();

        Resize(width, height);
    }

    public bool IsPaused { get; set; }

    public UpscalerKind Upscaler
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                Resize(outputPass.Color.Desc.Width, outputPass.Color.Desc.Height);
            }
        }
    }

    public UpscalingMode UpscalingMode
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                Resize(outputPass.Color.Desc.Width, outputPass.Color.Desc.Height);
            }
        }
    } = UpscalingMode.Quality;

    public bool IsFrameGenerationEnabled
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                Resize(outputPass.Color.Desc.Width, outputPass.Color.Desc.Height);
            }
        }
    }

    public Texture Overlay => outputPass.Overlay;

    public ReadOnlySpan<Texture> Render(CommandBuffer commandBuffer, CameraHandler camera, double deltaSeconds)
    {
        if (!IsPaused)
        {
            elapsedSeconds += deltaSeconds;
        }

        uint sample = frameIndex + 1u;
        Vector2 jitterOffset = upscalingPass is null ? Vector2.Zero : new(Halton(sample, 2u) - 0.5f, Halton(sample, 3u) - 0.5f);
        bool reset = frameIndex is 0;

        scene.Update(commandBuffer, elapsedSeconds);
        pathTracingPass.Record(commandBuffer, scene, camera, jitterOffset, frameIndex);

        Texture color = pathTracingPass.Output.Color;
        if (upscalingPass is not null)
        {
            upscalingPass.Record(commandBuffer, pathTracingPass.Output, camera, jitterOffset, reset);
            color = upscalingPass.Color;
        }

        outputPass.Record(commandBuffer, color, frameIndex);
        frameGenerationPass?.Record(commandBuffer, outputPass.Color, pathTracingPass.Output, camera, jitterOffset, deltaSeconds, reset);

        frameIndex++;

        if (frameGenerationPass is null || reset)
        {
            return outputPass.Composite(commandBuffer, [outputPass.Color]);
        }

        return outputPass.Composite(commandBuffer, [frameGenerationPass.Color, outputPass.Color]);
    }

    public void Resize(uint width, uint height)
    {
        upscalingPass?.Dispose();
        upscalingPass = Upscaler is UpscalerKind.Off ? null : new(Upscaler, UpscalingMode, width, height);

        UpscalingInputSize inputSize = upscalingPass?.InputSize ?? new(width, height);

        pathTracingPass.Resize(inputSize.Width, inputSize.Height);
        outputPass.Resize(width, height, IsFrameGenerationEnabled ? 2 : 1);

        frameGenerationPass?.Dispose();
        frameGenerationPass = IsFrameGenerationEnabled ? new(inputSize.Width, inputSize.Height, width, height) : null;

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

    private static float Halton(uint index, uint radix)
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
