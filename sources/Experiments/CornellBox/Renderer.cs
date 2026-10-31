using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using CornellBox.Passes;
using Zenith.NET;
using Zenith.NET.Extensions.Reconstruction;

namespace CornellBox;

internal class Renderer : DisposableObject
{
    private readonly SceneResources scene;
    private readonly PathTracingPass pathTracingPass;
    private readonly UpscalingPass upscalingPass;
    private readonly OutputPass outputPass;
    private readonly FrameGenerationPass frameGenerationPass;
    private readonly CompositePass compositePass;

    private Matrix4x4 previousView;
    private Matrix4x4 previousProjection;
    private int[] outputIndices = [0];
    private int firstOutputIndex;
    private double elapsedSeconds;
    private uint frameIndex;

    public Renderer()
    {
        RenderDesc desc = new()
        {
            InputWidth = App.Width,
            InputHeight = App.Height,
            OutputWidth = App.Width,
            OutputHeight = App.Height
        };

        scene = new();
        upscalingPass = new(desc);
        pathTracingPass = new(desc);
        outputPass = new(desc);
        frameGenerationPass = new(desc);
        compositePass = new(desc);
    }

    public bool IsPaused { get; set; }

    public UpscalerKind Upscaler { get; set; }

    public UpscalerMode UpscalerMode { get; set; } = UpscalerMode.Quality;

    public FrameGenerationMode FrameGeneration { get; set; }

    public Texture Overlay => compositePass.Overlay;

    public ReadOnlySpan<Texture> Colors => compositePass.Colors;

    public ReadOnlySpan<int> OutputIndices => outputIndices.AsSpan(firstOutputIndex);

    public void Update(double deltaSeconds)
    {
        if (!IsPaused)
        {
            elapsedSeconds += deltaSeconds;
        }
    }

    public void Render(CommandBuffer commandBuffer, CameraHandler camera, double deltaSeconds)
    {
        Resize(App.Width, App.Height);

        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        uint sample = frameIndex + 1u;
        RenderArgs args = new()
        {
            Scene = scene,
            Camera = camera,
            Input = pathTracingPass.Output,
            View = view,
            Projection = projection,
            PreviousView = frameIndex is 0 ? view : previousView,
            PreviousProjection = frameIndex is 0 ? projection : previousProjection,
            JitterOffset = upscalingPass.Color is null ? Vector2.Zero : new(Halton(sample, 2u) - 0.5f, Halton(sample, 3u) - 0.5f),
            DeltaSeconds = deltaSeconds,
            FrameIndex = frameIndex
        };

        scene.Update(commandBuffer, elapsedSeconds);

        pathTracingPass.Record(commandBuffer, args);
        upscalingPass.Record(commandBuffer, args);

        args = args with { Color = upscalingPass.Color ?? args.Input.Color };
        outputPass.Record(commandBuffer, args);

        args = args with { Color = outputPass.Color };
        frameGenerationPass.Record(commandBuffer, args);

        firstOutputIndex = args.Reset ? outputIndices.Length - 1 : 0;
        previousView = view;
        previousProjection = projection;
        frameIndex++;
    }

    public void Composite(CommandBuffer commandBuffer)
    {
        compositePass.Record(commandBuffer, new()
        {
            Color = outputPass.Color,
            Generated = frameGenerationPass.Colors
        });
    }

    public void Resize(uint width, uint height)
    {
        RenderDesc desc = new()
        {
            InputWidth = width,
            InputHeight = height,
            OutputWidth = width,
            OutputHeight = height,
            Upscaler = Upscaler,
            UpscalerMode = UpscalerMode,
            FrameGeneration = FrameGeneration
        };

        ReconstructionCapabilities capabilities = App.Context.ReconstructionCapabilities;
        bool isUpscalerSupported = desc.Upscaler switch
        {
            UpscalerKind.Temporal => capabilities.TemporalUpscalerSupported,
            UpscalerKind.Denoising => capabilities.DenoisingUpscalerSupported,
            _ => true
        };

        if (!isUpscalerSupported)
        {
            desc.Upscaler = UpscalerKind.Off;
        }

        if (!capabilities.FrameGeneratorSupported)
        {
            desc.FrameGeneration = FrameGenerationMode.X1;
        }

        if (outputPass.Color.Desc.Width == width && outputPass.Color.Desc.Height == height && upscalingPass.Desc.Upscaler == desc.Upscaler && upscalingPass.Desc.UpscalerMode == desc.UpscalerMode && frameGenerationPass.Colors.Length == (int)desc.FrameGeneration)
        {
            return;
        }

        upscalingPass.Resize(desc);
        desc.InputWidth = upscalingPass.Desc.InputWidth;
        desc.InputHeight = upscalingPass.Desc.InputHeight;

        pathTracingPass.Resize(desc);
        outputPass.Resize(desc);
        frameGenerationPass.Resize(desc);
        compositePass.Resize(desc);

        if (outputIndices.Length != Colors.Length)
        {
            outputIndices = [.. Enumerable.Range(0, Colors.Length)];
        }

        frameIndex = 0;
    }

    protected override void Destroy()
    {
        compositePass?.Dispose();
        frameGenerationPass?.Dispose();
        outputPass?.Dispose();
        pathTracingPass?.Dispose();
        upscalingPass?.Dispose();
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
