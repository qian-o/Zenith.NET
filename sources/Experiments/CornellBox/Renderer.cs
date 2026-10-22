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

    private DenoisingUpscalingPass denoisingUpscalingPass;
    private FrameGenerationPass frameGenerationPass;
    private double elapsedSeconds;
    private uint frameIndex;

    public Renderer()
    {
        scene = new();
        pathTracingPass = new(App.Width, App.Height);
        denoisingUpscalingPass = new(App.Width, App.Height);
        frameGenerationPass = new(App.Width, App.Height, App.Width, App.Height);
        outputPass = new(App.Width, App.Height);
    }

    public bool IsPaused { get; set; }

    public DenoisingUpscalingMode DenoisingUpscaling { get; set; }

    public FrameGenerationMode FrameGeneration { get; set; }

    public Texture Overlay => outputPass.Overlay;

    public ReadOnlySpan<Texture> GetColors()
    {
        outputPass.SelectOutputs(frameGenerationPass.ReadOutputIndices());

        return outputPass.Colors;
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

        pathTracingPass.JitterOffset = denoisingUpscalingPass.GetJitterOffset(frameIndex);

        scene.Update(commandBuffer, elapsedSeconds);

        pathTracingPass.Record(commandBuffer, scene, camera, frameIndex);
        denoisingUpscalingPass.Record(commandBuffer, pathTracingPass);
        outputPass.Record(commandBuffer, denoisingUpscalingPass.Color, frameIndex);
        frameGenerationPass.Record(commandBuffer, outputPass.SceneColor, pathTracingPass, camera);

        pathTracingPass.ResetHistory = false;
        frameIndex++;
    }

    public void Composite(CommandBuffer commandBuffer)
    {
        outputPass.Record(commandBuffer, frameGenerationPass.Colors);
    }

    public void Resize(uint width, uint height)
    {
        bool isDenoisingChanged = denoisingUpscalingPass.Mode != DenoisingUpscaling;
        bool isGenerationChanged = frameGenerationPass.Mode != FrameGeneration;
        if (outputPass.SceneColor.Desc.Width == width && outputPass.SceneColor.Desc.Height == height && !isDenoisingChanged && !isGenerationChanged)
        {
            return;
        }

        if (isDenoisingChanged)
        {
            denoisingUpscalingPass.Dispose();
            denoisingUpscalingPass = new(width, height, DenoisingUpscaling);

            pathTracingPass.ResetHistory = true;
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

        pathTracingPass.Resize(denoisingUpscalingPass.InputWidth, denoisingUpscalingPass.InputHeight);

        outputPass.Resize(width, height, frameGenerationPass.Colors.Length);
    }

    protected override void Destroy()
    {
        outputPass?.Dispose();
        frameGenerationPass?.Dispose();
        denoisingUpscalingPass?.Dispose();
        pathTracingPass?.Dispose();
        scene?.Dispose();
    }
}
