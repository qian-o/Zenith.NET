using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using CornellBox.Passes;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;

namespace CornellBox;

internal class Renderer : DisposableObject
{
    private readonly Scene scene;
    private readonly PathTracingPass pathTracing;
    private readonly RayReconstructionPass rayReconstruction;
    private readonly TonemapPass tonemap;
    private readonly CompositePass composite;
    private readonly FrameGenerationPass frameGeneration;

    private PassArgs? pendingComposite;
    private uint frameIndex;
    private Matrix4x4 previousViewProjection;
    private bool history;

    public Renderer()
    {
        scene = new();
        pathTracing = new(App.Width, App.Height, App.Width, App.Height);
        rayReconstruction = new(App.Width, App.Height, App.Width, App.Height);
        tonemap = new(App.Width, App.Height, App.Width, App.Height);
        composite = new(App.Width, App.Height, App.Width, App.Height);
        frameGeneration = new(App.Width, App.Height, App.Width, App.Height);

        switch (App.Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
            case GraphicsApi.Vulkan:
                DLSSCapabilities = App.Context.DLSSCapabilities;
                break;
        }
    }

    public DLSSCapabilities? DLSSCapabilities { get; }

    public bool IsPaused { get; set; }

    public DLSSMode? RayReconstruction { get; set; }

    public bool IsFrameGenerationEnabled { get; set; }

    public bool IsFrameGenerated => frameGeneration.IsGenerated;

    public void Render(CommandBuffer commandBuffer, CameraHandler camera, double delta, int slot, Texture ui, Texture backBuffer, Texture generatedFrame)
    {
        scene.Update(commandBuffer, IsPaused ? 0.0 : delta);

        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        Matrix4x4 viewProjection = view * projection;
        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);

        DLSSMode? reconstruction = DLSSCapabilities?.RayReconstructionSupported is true ? RayReconstruction : null;

        Vector2 jitter = reconstruction is not null ? new(Halton(frameIndex + 1, 2) - 0.5f, Halton(frameIndex + 1, 3) - 0.5f) : Vector2.Zero;

        if (!history)
        {
            previousViewProjection = viewProjection;
        }

        Matrix4x4 clipToPrevClip = viewProjection == previousViewProjection ? Matrix4x4.Identity : inverseProjection * (inverseView * previousViewProjection);

        PassArgs args = new()
        {
            Scene = scene,
            UI = ui,
            BackBuffer = backBuffer,
            GeneratedFrame = generatedFrame,
            View = view,
            Projection = projection,
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            ViewProjection = viewProjection,
            PreviousViewProjection = previousViewProjection,
            ClipToPrevClip = clipToPrevClip,
            CameraPosition = camera.Position,
            CameraUp = camera.Up,
            CameraRight = camera.Right,
            CameraForward = camera.Forward,
            CameraNear = camera.NearPlane,
            CameraFar = camera.FarPlane,
            CameraFovAngleVer = float.DegreesToRadians(camera.Fov),
            CameraAspectRatio = camera.AspectRatio,
            Jitter = jitter,
            FrameIndex = frameIndex,
            Slot = slot,
            RayReconstruction = reconstruction,
            FrameGeneration = IsFrameGenerationEnabled && DLSSCapabilities?.FrameGenerationSupported is true
        };

        pathTracing.Record(commandBuffer, in args);

        args = args with
        {
            Color = pathTracing.Color,
            Normal = pathTracing.Normal,
            Roughness = pathTracing.Roughness,
            Depth = pathTracing.Depth,
            MotionVectors = pathTracing.MotionVectors,
            DiffuseAlbedo = pathTracing.DiffuseAlbedo,
            SpecularAlbedo = pathTracing.SpecularAlbedo,
            SpecularHitDistance = pathTracing.SpecularHitDistance
        };

        rayReconstruction.Record(commandBuffer, in args);

        args = args with { Color = rayReconstruction.Color };
        tonemap.Record(commandBuffer, in args);

        args = args with { Color = tonemap.Color };
        frameGeneration.Record(commandBuffer, in args);

        if (args.FrameGeneration)
        {
            pendingComposite = args with
            {
                Color = frameGeneration.Color,
                GeneratedColor = frameGeneration.IsGenerated ? frameGeneration.GeneratedColor : null
            };
        }
        else
        {
            pendingComposite = null;
            composite.Record(commandBuffer, in args);
        }

        previousViewProjection = viewProjection;
        history = true;

        frameIndex++;
    }

    public TimelineValue GenerateFrame(TimelineValue rendered)
    {
        if (pendingComposite is not PassArgs args)
        {
            return rendered;
        }

        CommandBuffer commandBuffer = App.Context.ComputeQueue.CommandBuffer();

        frameGeneration.Dispatch(commandBuffer);
        composite.Record(commandBuffer, in args);

        pendingComposite = null;

        return commandBuffer.Submit(rendered);
    }

    public void Resize(uint width, uint height)
    {
        uint renderWidth = width;
        uint renderHeight = height;

        if (RayReconstruction is DLSSMode mode && DLSSCapabilities?.RayReconstructionSupported is true)
        {
            DLSSOptimalSettings settings = App.Context.GetDLSSRayReconstructionOptimalSettings(width, height, mode);

            if (settings.InputWidth is not 0 && settings.InputHeight is not 0)
            {
                renderWidth = settings.InputWidth;
                renderHeight = settings.InputHeight;
            }
        }

        if (pathTracing.RenderWidth == renderWidth && pathTracing.RenderHeight == renderHeight && pathTracing.DisplayWidth == width && pathTracing.DisplayHeight == height)
        {
            return;
        }

        pathTracing.Resize(renderWidth, renderHeight, width, height);
        rayReconstruction.Resize(renderWidth, renderHeight, width, height);
        tonemap.Resize(renderWidth, renderHeight, width, height);
        composite.Resize(renderWidth, renderHeight, width, height);
        frameGeneration.Resize(renderWidth, renderHeight, width, height);

        history = false;
    }

    protected override void Destroy()
    {
        frameGeneration.Dispose();
        composite.Dispose();
        tonemap.Dispose();
        rayReconstruction.Dispose();
        pathTracing.Dispose();
        scene.Dispose();
    }

    private static float Halton(uint index, uint radix)
    {
        float result = 0.0f;
        float fraction = 1.0f;

        while (index is not 0)
        {
            fraction /= radix;
            result += fraction * (index % radix);
            index /= radix;
        }

        return result;
    }
}
