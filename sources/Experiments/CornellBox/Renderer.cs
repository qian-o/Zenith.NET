using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using CornellBox.Passes;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox;

internal class Renderer : DisposableObject
{
    private readonly SceneResources scene;
    private readonly PathTracingPass pathTracingPass;
    private readonly DenoisingUpscalingPass denoisingUpscalingPass;
    private readonly OutputPass outputPass;
    private readonly FrameGenerationPass frameGenerationPass;
    private readonly CompositePass compositePass;

    private Matrix4x4 previousView;
    private Matrix4x4 previousProjection;
    private Texture?[] outputs = [null];
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
        denoisingUpscalingPass = new(desc);
        pathTracingPass = new(desc);
        outputPass = new(desc);
        frameGenerationPass = new(desc);
        compositePass = new(desc);
    }

    public bool IsPaused { get; set; }

    public DenoisingUpscalingMode DenoisingUpscaling { get; set; }

    public FrameGenerationMode FrameGeneration { get; set; }

    public Texture Overlay => compositePass.Overlay;

    public void Update(double deltaSeconds)
    {
        if (!IsPaused)
        {
            elapsedSeconds += deltaSeconds;
        }
    }

    public void Render(CommandBuffer commandBuffer, CameraHandler camera)
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
            JitterOffset = denoisingUpscalingPass.Color is null ? Vector2.Zero : new(Halton(sample, 2u) - 0.5f, Halton(sample, 3u) - 0.5f),
            FrameIndex = frameIndex
        };

        scene.Update(commandBuffer, elapsedSeconds);

        pathTracingPass.Record(commandBuffer, args);
        denoisingUpscalingPass.Record(commandBuffer, args);

        args = args with { Color = denoisingUpscalingPass.Color ?? args.Input.Color };
        outputPass.Record(commandBuffer, args);

        args = args with { Color = outputPass.Color };
        frameGenerationPass.Record(commandBuffer, args);

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

    public ReadOnlySpan<Texture?> ReadOutputs(TimelineValue completion)
    {
        ReadOnlySpan<Texture> colors = compositePass.Colors;
        outputs[^1] = colors[^1];

        if (frameGenerationPass.Readback is not Buffer readback)
        {
            return outputs;
        }

        completion.Wait();

        unsafe
        {
            byte* flags = (byte*)readback.Map();

            try
            {
                for (int i = 0; i < colors.Length - 1; i++)
                {
                    outputs[i] = flags[i * sizeof(uint)] is 0 ? colors[i] : null;
                }
            }
            finally
            {
                readback.Unmap();
            }
        }

        return outputs;
    }

    public void Resize(uint width, uint height)
    {
        RenderDesc desc = new()
        {
            InputWidth = width,
            InputHeight = height,
            OutputWidth = width,
            OutputHeight = height,
            DenoisingUpscaling = DenoisingUpscaling,
            FrameGeneration = FrameGeneration
        };
        if (App.Technology is not RenderingTechnology.DLSS)
        {
            desc.DenoisingUpscaling = DenoisingUpscalingMode.Off;
            desc.FrameGeneration = FrameGenerationMode.X1;
        }

        if (outputPass.Color.Desc.Width == width && outputPass.Color.Desc.Height == height && denoisingUpscalingPass.Desc.DenoisingUpscaling == desc.DenoisingUpscaling && frameGenerationPass.Colors.Length == (int)desc.FrameGeneration)
        {
            return;
        }

        denoisingUpscalingPass.Resize(desc);
        desc.InputWidth = denoisingUpscalingPass.Desc.InputWidth;
        desc.InputHeight = denoisingUpscalingPass.Desc.InputHeight;

        pathTracingPass.Resize(desc);
        outputPass.Resize(desc);
        frameGenerationPass.Resize(desc);
        compositePass.Resize(desc);

        if (outputs.Length != compositePass.Colors.Length)
        {
            outputs = new Texture?[compositePass.Colors.Length];
        }

        frameIndex = 0;
    }

    protected override void Destroy()
    {
        compositePass?.Dispose();
        frameGenerationPass?.Dispose();
        outputPass?.Dispose();
        pathTracingPass?.Dispose();
        denoisingUpscalingPass?.Dispose();
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
