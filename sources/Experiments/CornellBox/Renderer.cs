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
    private readonly GBufferPass gBufferPass;
    private readonly GIProbePass giProbePass;
    private readonly ShadowPass shadowPass;
    private readonly LightingPass lightingPass;
    private readonly OutputPass outputPass;

    private UpscalingPass? upscalingPass;
    private double elapsedSeconds;
    private uint frameIndex;
    private bool isFrameGenerationEnabled;
    private bool hasPendingLightingReset;
    private Matrix4x4 previousViewProjection = Matrix4x4.Identity;

    public Renderer(bool isFrameGenerationEnabled)
    {
        scene = new();
        gBufferPass = new();
        giProbePass = new();
        shadowPass = new();
        lightingPass = new();
        outputPass = new();

        Resize(isFrameGenerationEnabled);
    }

    public UpscalingQuality? Quality { get; set; } = App.Context.UpscalingCapabilities.SuperResolutionSupported ? UpscalingQuality.Quality : null;

    public AtmospherePreset Preset
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                hasPendingLightingReset = true;
            }
        }
    } = AtmospherePreset.Day;

    public bool IsPaused { get; set; }

    public Texture Color => outputPass.Color;

    public void Update(double deltaSeconds)
    {
        if (!IsPaused)
        {
            elapsedSeconds += deltaSeconds;
        }
    }

    public void Reset()
    {
        IsPaused = false;
        elapsedSeconds = 0.0;
        frameIndex = 0;
        hasPendingLightingReset = false;
    }

    public FrameGenerationSwapChainArgs Render(CommandBuffer commandBuffer, CameraHandler camera, double deltaSeconds)
    {
        bool reset = frameIndex is 0;
        bool resetLighting = reset || hasPendingLightingReset;
        hasPendingLightingReset = false;

        uint renderWidth = upscalingPass?.RenderWidth ?? App.PhysicalWidth;
        uint renderHeight = upscalingPass?.RenderHeight ?? App.PhysicalHeight;
        FrameContext frame = FrameContext.Create(camera,
                                                 SceneEnvironment.Create(Preset, (float)elapsedSeconds, IsPaused),
                                                 previousViewProjection,
                                                 frameIndex,
                                                 renderWidth,
                                                 renderHeight,
                                                 App.PhysicalWidth,
                                                 App.PhysicalHeight,
                                                 (float)deltaSeconds,
                                                 upscalingPass is not null,
                                                 reset,
                                                 resetLighting);

        scene.Update(commandBuffer, elapsedSeconds, IsPaused, frame.Reset);

        gBufferPass.Record(commandBuffer, scene, in frame);

        giProbePass.Record(commandBuffer, scene, in frame);

        shadowPass.Record(commandBuffer, scene, gBufferPass, in frame);

        lightingPass.Record(commandBuffer, scene, gBufferPass, giProbePass, shadowPass, in frame);

        Texture color = lightingPass.Color;

        if (upscalingPass is not null)
        {
            upscalingPass.Record(commandBuffer, gBufferPass, lightingPass, in frame);
            color = upscalingPass.Color;
        }

        outputPass.Record(commandBuffer, color, in frame);

        previousViewProjection = frame.ViewProjection;
        frameIndex++;

        bool useOutputMotionDepth = isFrameGenerationEnabled && gBufferPass.HasOutputMotionDepth;

        return new()
        {
            Depth = useOutputMotionDepth ? gBufferPass.FrameGenerationDepth : gBufferPass.Depth,
            MotionVectors = useOutputMotionDepth ? gBufferPass.FrameGenerationMotionVectors : gBufferPass.MotionVectors,
            Hudless = outputPass.Color,
            JitterOffset = useOutputMotionDepth ? Vector2.Zero : frame.JitterOffset,
            View = frame.View,
            Projection = frame.Projection,
            DeltaTimeInSeconds = frame.DeltaTimeInSeconds,
            Reset = frame.ResetLighting
        };
    }

    public void Resize(bool isFrameGenerationEnabled)
    {
        this.isFrameGenerationEnabled = isFrameGenerationEnabled;

        if (Quality is UpscalingQuality quality)
        {
            if (upscalingPass is null || upscalingPass.Quality != quality)
            {
                upscalingPass?.Dispose();
                upscalingPass = new(quality);
            }
            else
            {
                upscalingPass.Resize();
            }
        }
        else
        {
            upscalingPass?.Dispose();
            upscalingPass = null;
        }

        uint renderWidth = upscalingPass?.RenderWidth ?? App.PhysicalWidth;
        uint renderHeight = upscalingPass?.RenderHeight ?? App.PhysicalHeight;

        gBufferPass.Resize(renderWidth, renderHeight, App.PhysicalWidth, App.PhysicalHeight, isFrameGenerationEnabled);
        shadowPass.Resize(renderWidth, renderHeight);
        lightingPass.Resize(renderWidth, renderHeight, upscalingPass is not null);
        outputPass.Resize(App.PhysicalWidth, App.PhysicalHeight, upscalingPass is null);

        frameIndex = 0;
    }

    protected override void Destroy()
    {
        upscalingPass?.Dispose();
        upscalingPass = null;
        outputPass?.Dispose();
        lightingPass?.Dispose();
        shadowPass?.Dispose();
        giProbePass?.Dispose();
        gBufferPass?.Dispose();
        scene?.Dispose();
    }
}
