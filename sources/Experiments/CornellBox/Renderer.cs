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
        pathTracing = new();
        denoise = new();
        upscale = new();
        tonemap = new();

        Resize(App.Width, App.Height);
    }

    public float RenderPrecision
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                Resize(App.Width, App.Height);
            }
        }
    } = 0.67f;

    public UpscaleMode UpscaleMode
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                uint renderWidth = Math.Max((uint)(App.Width * RenderPrecision), 1);
                uint renderHeight = Math.Max((uint)(App.Height * RenderPrecision), 1);

                upscale.Resize(renderWidth, renderHeight, App.Width, App.Height, UpscaleMode);
            }
        }
    } = UpscaleMode.Temporal;

    public Texture Color => tonemap.Color;

    public void Render(CommandBuffer commandBuffer, CameraHandler camera)
    {
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

        pathTracing.Render(commandBuffer, camera, previousViewProjection, jitter, frameIndex);
        denoise.Render(commandBuffer, pathTracing.Color, pathTracing.Normal, pathTracing.Depth, jitter, previousJitter, sameCamera, clipToPrevClip);

        Texture hdr = upscale.Render(commandBuffer,
                                     UpscaleMode is UpscaleMode.Temporal ? denoise.Color : denoise.ResolvedColor,
                                     pathTracing.Depth,
                                     pathTracing.MotionVectors,
                                     jitter,
                                     clipToPrevClip,
                                     2.0f * MathF.Atan(MathF.Tan(float.DegreesToRadians(camera.Fov) * 0.5f) * camera.AspectRatio),
                                     sameCamera);

        tonemap.Render(commandBuffer, hdr, frameIndex);

        previousViewProjection = viewProjection;
        previousJitter = jitter;
        history = true;

        frameIndex++;
    }

    public void Resize(uint width, uint height)
    {
        uint renderWidth = Math.Max((uint)(width * RenderPrecision), 1);
        uint renderHeight = Math.Max((uint)(height * RenderPrecision), 1);

        pathTracing.Resize(renderWidth, renderHeight);
        denoise.Resize(renderWidth, renderHeight);
        upscale.Resize(renderWidth, renderHeight, width, height, UpscaleMode);
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
