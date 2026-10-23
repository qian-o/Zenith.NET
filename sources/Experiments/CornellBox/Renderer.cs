using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using CornellBox.Passes;
using Zenith.NET;

namespace CornellBox;

internal class Renderer : DisposableObject
{
    private readonly SceneResources scene;
    private readonly PathTracingPass pathTracingPass;
    private readonly OutputPass outputPass;
    private readonly CompositePass compositePass;

    private DenoisingUpscalingPass denoisingUpscalingPass;
    private FrameGenerationPass frameGenerationPass;
    private Matrix4x4 previousViewProjection;
    private bool hasPendingReset = true;
    private double elapsedSeconds;
    private uint frameIndex;

    public Renderer()
    {
        scene = new();
        pathTracingPass = new(App.Width, App.Height);
        denoisingUpscalingPass = new(App.Width, App.Height);
        frameGenerationPass = new(App.Width, App.Height, App.Width, App.Height);
        outputPass = new(App.Width, App.Height);
        compositePass = new(App.Width, App.Height, 1);
    }

    public bool IsPaused { get; set; }

    public DenoisingUpscalingMode DenoisingUpscaling { get; set; }

    public FrameGenerationMode FrameGeneration { get; set; }

    public Texture Overlay => compositePass.Overlay;

    public ReadOnlySpan<Texture> Colors => compositePass.Colors;

    public ReadOnlySpan<int> ReadOutputIndices(TimelineValue completion)
    {
        return frameGenerationPass.ReadOutputIndices(completion);
    }

    public void Update(double deltaSeconds)
    {
        if (!IsPaused)
        {
            elapsedSeconds += deltaSeconds;
        }
    }

    public void Render(CommandBuffer commandBuffer, CameraHandler camera)
    {
        if (denoisingUpscalingPass.Mode != DenoisingUpscaling || frameGenerationPass.Mode != FrameGeneration)
        {
            Resize(App.Width, App.Height);
        }

        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        Matrix4x4 viewProjection = view * projection;
        RenderArgs args = new()
        {
            Camera = camera,
            View = view,
            Projection = projection,
            ViewProjection = viewProjection,
            PreviousViewProjection = hasPendingReset ? viewProjection : previousViewProjection,
            JitterOffset = denoisingUpscalingPass.GetJitterOffset(frameIndex),
            FrameIndex = frameIndex,
            Reset = hasPendingReset
        };

        scene.Update(commandBuffer, elapsedSeconds);

        pathTracingPass.Record(commandBuffer, scene, args);
        Texture color = denoisingUpscalingPass.Record(commandBuffer, pathTracingPass, args);
        outputPass.Record(commandBuffer, color, frameIndex);
        frameGenerationPass.Record(commandBuffer, outputPass.Color, pathTracingPass, args);

        previousViewProjection = viewProjection;
        hasPendingReset = false;
        frameIndex++;
    }

    public void Composite(CommandBuffer commandBuffer)
    {
        compositePass.Record(commandBuffer, frameGenerationPass.Colors);
    }

    public void Resize(uint width, uint height)
    {
        bool isDenoisingChanged = denoisingUpscalingPass.Mode != DenoisingUpscaling;
        bool isGenerationChanged = frameGenerationPass.Mode != FrameGeneration;
        if (outputPass.Color.Desc.Width == width && outputPass.Color.Desc.Height == height && !isDenoisingChanged && !isGenerationChanged)
        {
            return;
        }

        if (isDenoisingChanged)
        {
            denoisingUpscalingPass.Dispose();
            denoisingUpscalingPass = new(width, height, DenoisingUpscaling);
        }
        else
        {
            denoisingUpscalingPass.Resize(width, height);
        }

        if (isGenerationChanged)
        {
            frameGenerationPass.Dispose();
            frameGenerationPass = new(denoisingUpscalingPass.InputWidth, denoisingUpscalingPass.InputHeight, width, height, FrameGeneration);
        }
        else
        {
            frameGenerationPass.Resize(denoisingUpscalingPass.InputWidth, denoisingUpscalingPass.InputHeight, width, height);
        }

        hasPendingReset |= isDenoisingChanged || pathTracingPass.Width != denoisingUpscalingPass.InputWidth || pathTracingPass.Height != denoisingUpscalingPass.InputHeight;

        pathTracingPass.Resize(denoisingUpscalingPass.InputWidth, denoisingUpscalingPass.InputHeight);

        outputPass.Resize(width, height);
        compositePass.Resize(width, height, frameGenerationPass.Colors.Length);
    }

    protected override void Destroy()
    {
        compositePass?.Dispose();
        outputPass?.Dispose();
        frameGenerationPass?.Dispose();
        denoisingUpscalingPass?.Dispose();
        pathTracingPass?.Dispose();
        scene?.Dispose();
    }
}
