using System.Numerics;
using Sponza.Handlers;
using Sponza.Helpers;
using Sponza.Models;
using Sponza.Passes;
using Zenith.NET;

namespace Sponza;

internal class Renderer : IDisposable
{
    private const float Exposure = 1.5f;
    private const float AmbientOcclusionRadiusInMeters = 0.45f;
    private const float AmbientOcclusionStrength = 1.6f;

    private readonly GraphicsContext context;
    private readonly SceneResources scene;
    private readonly EnvironmentPass environmentPass;
    private readonly ShadowPass shadowPass;
    private readonly ScenePass scenePass;
    private readonly AmbientOcclusionPass ambientOcclusionPass;
    private readonly ToneMappingPass toneMappingPass;
    private readonly UpscalingPass upscalingPass;
    private readonly Queue<(Texture Texture, uint LastFrame)> retiredColors = [];

    private Texture? renderLdr;
    private TextureLayout colorLayout;
    private TextureLayout renderLdrLayout;
    private RenderSettings frameSettings;
    private FrameData frame;
    private FrameData previousFrame;
    private SkyData sky;
    private uint frameIndex;
    private uint jitterIndex;
    private uint renderWidth;
    private uint renderHeight;
    private float previousTimeOfDay;
    private bool initialized;
    private bool historyInvalid = true;

    public RenderSettings Settings;

    public Renderer()
    {
        context = App.Context;
        Color = CreateColor(App.Width, App.Height);

        scene = new(context);
        environmentPass = new(context);
        shadowPass = new(context);
        scenePass = new(context);
        ambientOcclusionPass = new(context);
        toneMappingPass = new(context);
        upscalingPass = new(context);
    }

    public Texture Color { get; private set; }

    public void Update(CameraHandler camera)
    {
        while (retiredColors.TryPeek(out (Texture Texture, uint LastFrame) retired) && retired.LastFrame < frameIndex)
        {
            retiredColors.Dequeue().Texture.Dispose();
        }

        RenderSettings settings = Settings;
        uint width = Math.Max(1, (uint)MathF.Floor(Color.Desc.Width * settings.RenderScale));
        uint height = Math.Max(1, (uint)MathF.Floor(Color.Desc.Height * settings.RenderScale));
        bool modeChanged = !initialized || settings.UpscalingMode != frameSettings.UpscalingMode;
        bool renderSizeChanged = renderWidth != width || renderHeight != height;

        if (renderSizeChanged)
        {
            scenePass.Resize(width, height);
            ambientOcclusionPass.Resize(width, height);
            renderWidth = width;
            renderHeight = height;
            historyInvalid = true;
        }

        if (!initialized || renderSizeChanged || modeChanged)
        {
            renderLdr?.Dispose();
            bool needsLdr = settings.UpscalingMode is UpscalingMode.Spatial || (settings.UpscalingMode is UpscalingMode.None && (width != Color.Desc.Width || height != Color.Desc.Height));
            renderLdr = needsLdr ? GraphicsHelper.CreateTexture(context, PixelFormat.R8G8B8A8UNorm, width, height, TextureUsages.Sampled | TextureUsages.ColorAttachment) : null;
            renderLdrLayout = TextureLayout.Undefined;
            historyInvalid = true;
        }

        upscalingPass.Configure(width, height, Color.Desc.Width, Color.Desc.Height, settings.UpscalingMode);

        Matrix4x4 view = camera.View;
        Matrix4x4 unjitteredProjection = camera.Projection;
        Matrix4x4 unjitteredViewProjection = view * unjitteredProjection;
        bool reset = historyInvalid || frameIndex is 0 || settings.UpscalingMode != frameSettings.UpscalingMode || MathF.Abs(settings.TimeOfDay - previousTimeOfDay) > 1.0f;
        reset |= frameIndex is not 0 && (Vector3.DistanceSquared(camera.Position, previousFrame.CameraPositionWorld) > 16.0f || Vector3.Dot(camera.Forward, previousFrame.CameraForwardWorld) < 0.25f || unjitteredProjection != previousFrame.UnjitteredProjection);

        uint phase = reset ? 0 : jitterIndex;
        Vector2 jitter = settings.UpscalingMode is UpscalingMode.Temporal ? new(Halton(phase % 8 + 1, 2) - 0.5f, Halton(phase % 8 + 1, 3) - 0.5f) : Vector2.Zero;
        Matrix4x4 jitterMatrix = Matrix4x4.Identity;
        jitterMatrix.M41 = 2.0f * jitter.X / width;
        jitterMatrix.M42 = -2.0f * jitter.Y / height;
        Matrix4x4 projection = unjitteredProjection * jitterMatrix;
        Matrix4x4 viewProjection = unjitteredViewProjection * jitterMatrix;
        Matrix4x4 previousViewProjection = reset ? unjitteredViewProjection : previousFrame.UnjitteredViewProjection;

        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);
        Matrix4x4.Invert(viewProjection, out Matrix4x4 inverseViewProjection);

        frame = new()
        {
            View = view,
            Projection = projection,
            UnjitteredProjection = unjitteredProjection,
            ViewProjection = viewProjection,
            UnjitteredViewProjection = unjitteredViewProjection,
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            InverseViewProjection = inverseViewProjection,
            PreviousViewProjection = previousViewProjection,
            PreviousView = reset ? view : previousFrame.View,
            PreviousProjection = reset ? unjitteredProjection : previousFrame.UnjitteredProjection,
            ClipToPrevClip = inverseViewProjection * (previousViewProjection * jitterMatrix),
            CameraPositionWorld = camera.Position,
            CameraForwardWorld = camera.Forward,
            JitterInPixels = jitter,
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            DisplayWidth = Color.Desc.Width,
            DisplayHeight = Color.Desc.Height,
            FrameIndex = frameIndex,
            HorizontalHalfFovTan = 1.0f / unjitteredProjection.M11,
            Reset = reset,
            SameCamera = !reset && unjitteredViewProjection == previousFrame.UnjitteredViewProjection
        };

        frameSettings = settings;
        sky = CreateSky(settings.TimeOfDay);

        if (frameIndex is 0 || settings.TimeOfDay != previousTimeOfDay)
        {
            environmentPass.Invalidate();
            shadowPass.Invalidate();
        }

        initialized = true;
    }

    public void Render(CommandBuffer commandBuffer)
    {
        EnvironmentData environment = environmentPass.Record(commandBuffer, sky);

        ShadowData shadow = shadowPass.Record(commandBuffer, new() { Scene = scene.Data, Sky = sky });

        SceneOutput sceneOutput = scenePass.Record(commandBuffer, new()
        {
            Scene = scene.Data,
            Frame = frame,
            Sky = sky,
            Shadow = shadow,
            Environment = environment
        });

        Texture hdr = ambientOcclusionPass.Record(commandBuffer, new()
        {
            HdrColor = sceneOutput.HdrColor,
            IndirectDiffuse = sceneOutput.IndirectDiffuse,
            DeviceDepth = sceneOutput.DeviceDepth,
            Frame = frame,
            RadiusInMeters = AmbientOcclusionRadiusInMeters,
            Strength = AmbientOcclusionStrength
        });

        if (frameSettings.UpscalingMode is UpscalingMode.Temporal)
        {
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            hdr = upscalingPass.RecordTemporal(commandBuffer, new()
            {
                HdrColor = hdr,
                DeviceDepth = sceneOutput.DeviceDepth,
                EncodedMotion = sceneOutput.EncodedMotion,
                Frame = frame
            });

            toneMappingPass.Record(commandBuffer, new() { HdrColor = hdr, Exposure = Exposure, Target = Color, TargetLayout = colorLayout });
        }
        else
        {
            toneMappingPass.Record(commandBuffer, new()
            {
                HdrColor = hdr,
                Exposure = Exposure,
                Target = renderLdr ?? Color,
                TargetLayout = renderLdr is null ? colorLayout : renderLdrLayout
            });

            if (renderLdr is not null)
            {
                renderLdrLayout = TextureLayout.Sampled;
                UpscalingPassArgs args = new() { Input = renderLdr, Target = Color, TargetLayout = colorLayout };

                if (frameSettings.UpscalingMode is UpscalingMode.Spatial)
                {
                    upscalingPass.RecordSpatial(commandBuffer, args);
                }
                else
                {
                    upscalingPass.RecordBilinear(commandBuffer, args);
                }
            }
        }

        colorLayout = TextureLayout.Sampled;
        previousFrame = frame;
        previousTimeOfDay = frameSettings.TimeOfDay;
        frameIndex++;
        jitterIndex = frameSettings.UpscalingMode is UpscalingMode.Temporal ? frame.Reset ? 1 : (jitterIndex + 1) % 8 : 0;
        historyInvalid = false;
    }

    public void Resize(uint width, uint height)
    {
        if (Color.Desc.Width == width && Color.Desc.Height == height)
        {
            return;
        }

        retiredColors.Enqueue((Color, frameIndex + 1));
        Color = CreateColor(width, height);
        colorLayout = TextureLayout.Undefined;
        historyInvalid = true;
        initialized = false;
    }

    public void Dispose()
    {
        upscalingPass.Dispose();
        toneMappingPass.Dispose();
        ambientOcclusionPass.Dispose();
        scenePass.Dispose();
        shadowPass.Dispose();
        environmentPass.Dispose();
        scene.Dispose();
        renderLdr?.Dispose();

        while (retiredColors.TryDequeue(out (Texture Texture, uint LastFrame) retired))
        {
            retired.Texture.Dispose();
        }

        Color.Dispose();
    }

    private Texture CreateColor(uint width, uint height)
    {
        return GraphicsHelper.CreateTexture(context, PixelFormat.R8G8B8A8UNorm, width, height, TextureUsages.Sampled | TextureUsages.ColorAttachment | TextureUsages.Storage);
    }

    private static SkyData CreateSky(float timeOfDay)
    {
        float angle = (timeOfDay - 6.0f) * MathF.PI / 12.0f;
        float elevation = MathF.Max(0.0f, MathF.Sin(angle));
        float daylight = MathF.Sqrt(elevation);
        Vector3 sunDirection = timeOfDay switch
        {
            6.0f => -Vector3.UnitZ,
            12.0f => Vector3.UnitY,
            18.0f => Vector3.UnitZ,
            _ => Vector3.Normalize(new(0.0f, elevation, -MathF.Cos(angle)))
        };

        return new()
        {
            SunDirectionWorld = sunDirection,
            SunRadiance = Vector3.Lerp(new(2.6f, 0.9f, 0.32f), new(3.6f, 3.35f, 2.95f), daylight) * (0.1f + 0.9f * daylight),
            ZenithColor = Vector3.Lerp(new(0.055f, 0.095f, 0.2f), new(0.12f, 0.28f, 0.55f), daylight),
            HorizonColor = Vector3.Lerp(new(0.55f, 0.25f, 0.12f), new(0.65f, 0.75f, 0.9f), daylight),
            GroundColor = new(0.10f, 0.085f, 0.065f),
            SkyIntensity = 0.35f + 0.65f * daylight
        };
    }

    private static float Halton(uint index, uint radix)
    {
        float value = 0.0f;
        float fraction = 1.0f;

        while (index is not 0)
        {
            fraction /= radix;
            value += fraction * (index % radix);
            index /= radix;
        }

        return value;
    }
}
