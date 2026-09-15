using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using CornellBox.Passes;
using Zenith.NET;

namespace CornellBox;

internal class Renderer : DisposableObject
{
    private readonly PathTracingPass pathTracing;
    private readonly DenoisePass denoise;
    private readonly UpscalePass upscale;
    private readonly TonemapPass tonemap;

    private uint frameIndex;
    private Matrix4x4 previousViewProjection;
    private Vector2 previousJitter;
    private bool history;

    public Renderer()
    {
        uint width = App.Width;
        uint height = App.Height;
        uint renderWidth = Math.Max((uint)(width * RenderPrecision), 1);
        uint renderHeight = Math.Max((uint)(height * RenderPrecision), 1);

        pathTracing = new(renderWidth, renderHeight);
        denoise = new(renderWidth, renderHeight);
        upscale = new(width, height);
        tonemap = new(width, height);
    }

    public float RenderPrecision { get; set; } = 0.67f;

    public UpscaleMode UpscaleMode { get; set; } = UpscaleMode.Temporal;

    public Texture Color => tonemap.Color;

    public void Render(CommandBuffer commandBuffer, CameraHandler camera)
    {
        Resize(Color.Desc.Width, Color.Desc.Height);

        UpscaleMode upscaleMode = UpscaleMode;
        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        Matrix4x4 viewProjection = view * projection;
        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);

        Vector2 jitter = new(Halton(frameIndex + 1, 2) - 0.5f, Halton(frameIndex + 1, 3) - 0.5f);

        if (!history)
        {
            previousViewProjection = viewProjection;
            previousJitter = jitter;
        }

        bool sameCamera = viewProjection == previousViewProjection;
        Matrix4x4 clipToPrevClip = sameCamera ? Matrix4x4.Identity : inverseProjection * (inverseView * previousViewProjection);

        PassArgs args = new()
        {
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            ViewProjection = viewProjection,
            PreviousViewProjection = previousViewProjection,
            ClipToPrevClip = clipToPrevClip,
            CameraPosition = camera.Position,
            CameraFovAngleHor = 2.0f * MathF.Atan(MathF.Tan(float.DegreesToRadians(camera.Fov) * 0.5f) * camera.AspectRatio),
            Jitter = jitter,
            PreviousJitter = previousJitter,
            FrameIndex = frameIndex,
            SameCamera = sameCamera,
            UpscaleMode = upscaleMode
        };

        pathTracing.Record(commandBuffer, in args);

        args = args with
        {
            Color = pathTracing.Color,
            Normal = pathTracing.Normal,
            Depth = pathTracing.Depth,
            MotionVectors = pathTracing.MotionVectors
        };
        denoise.Record(commandBuffer, in args);

        args = args with
        {
            Color = denoise.Color,
            ResolvedColor = denoise.ResolvedColor
        };
        upscale.Record(commandBuffer, in args);

        args = args with { Color = upscale.Color };
        tonemap.Record(commandBuffer, in args);

        previousViewProjection = viewProjection;
        previousJitter = jitter;
        history = true;

        frameIndex++;
    }

    public void Resize(uint width, uint height)
    {
        float renderPrecision = RenderPrecision;
        uint renderWidth = Math.Max((uint)(width * renderPrecision), 1);
        uint renderHeight = Math.Max((uint)(height * renderPrecision), 1);
        Texture pathColor = pathTracing.Color;

        if (pathColor.Desc.Width == renderWidth && pathColor.Desc.Height == renderHeight &&
            Color.Desc.Width == width && Color.Desc.Height == height)
        {
            return;
        }

        pathTracing.Resize(renderWidth, renderHeight);
        denoise.Resize(renderWidth, renderHeight);
        upscale.Resize(width, height);
        tonemap.Resize(width, height);

        history = false;
    }

    protected override void Destroy()
    {
        tonemap.Dispose();
        upscale.Dispose();
        denoise.Dispose();
        pathTracing.Dispose();
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
