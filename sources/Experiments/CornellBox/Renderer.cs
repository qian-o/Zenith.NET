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

    private double elapsedSeconds;
    private uint frameIndex;

    public Renderer()
    {
        scene = new();
        pathTracingPass = new(App.Width, App.Height);
        outputPass = new(App.Width, App.Height);
    }

    public bool IsPaused { get; set; }

    public Texture Color => outputPass.Color;

    public void Update(double deltaSeconds)
    {
        if (!IsPaused)
        {
            elapsedSeconds += deltaSeconds;
        }
    }

    public void Render(CommandBuffer commandBuffer, CameraHandler camera)
    {
        scene.Update(commandBuffer, elapsedSeconds);

        pathTracingPass.Record(commandBuffer, scene, camera, frameIndex);
        outputPass.Record(commandBuffer, pathTracingPass.Color, frameIndex);

        frameIndex++;
    }

    public void Resize(uint width, uint height)
    {
        if (Color.Desc.Width == width && Color.Desc.Height == height)
        {
            return;
        }

        pathTracingPass.Resize(width, height);
        outputPass.Resize(width, height);
    }

    protected override void Destroy()
    {
        outputPass?.Dispose();
        pathTracingPass?.Dispose();
        scene?.Dispose();
    }
}
