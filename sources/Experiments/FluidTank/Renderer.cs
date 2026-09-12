using System.Numerics;
using FluidTank.Handlers;
using FluidTank.Helpers;
using FluidTank.Models;
using FluidTank.Passes;
using Zenith.NET;

namespace FluidTank;

internal class Renderer : IDisposable
{
    private const double SimulationStep = 1.0 / 30.0;

    private readonly GraphicsContext context;

    private readonly Simulation simulation;

    private readonly SceneResources scene;

    private readonly ScenePass scenePass;

    private readonly SurfacePass surfacePass;

    private readonly WaterPass waterPass;

    private readonly GlassPass glassPass;

    private readonly OutputPass outputPass;

    private FrameData frame;

    private double simulationTime;

    private double accumulator = SimulationStep;

    private TimelineValue simulationReady;

    public RenderSettings Settings = new()
    {
        SurfaceScale = 0.5f,
        Clarity = 1.05f,
        RefractionStrength = 0.45f,
        Exposure = 1.0f,
        RayTracingEnabled = true,
        AntialiasingEnabled = true
    };

    public SimulationSettings SimulationSettings = new()
    {
        FlipRatio = 0.97f,
        VelocityDamping = 0.9998f,
        WaveAmplitude = 0.12f,
        WaveFrequency = 0.58f,
        PressureIterations = 18
    };

    public bool Paused;

    public Renderer(GraphicsContext context, uint width, uint height)
    {
        this.context = context;

        simulation = new(context);
        scene = new(context);
        scenePass = new(context);
        surfacePass = new(context);
        waterPass = new(context);
        glassPass = new(context);
        outputPass = new(context);

        Resize(width, height);
    }

    public Texture Color { get; private set; } = null!;

    public uint ParticleCount => simulation.ParticleCount;

    public void Update(CameraHandler camera, double delta)
    {
        frame.View = camera.View;
        frame.Projection = camera.Projection;
        Matrix4x4.Invert(frame.View, out frame.InvView);
        Matrix4x4.Invert(frame.Projection, out frame.InvProjection);
        frame.Position = camera.Position;
        frame.Right = camera.Right;
        frame.Up = camera.Up;
        frame.SunDirection = Vector3.Normalize(new(-0.38f, -0.83f, -0.42f));
        frame.LightIntensity = 2.7f;

        if (!Paused)
        {
            accumulator = Math.Min(accumulator + Math.Min(delta, SimulationStep), SimulationStep * 2.0);
        }

        ResizeSurface();
    }

    public void PushFluid(Vector3 origin, Vector3 direction)
    {
        simulation.Push(origin, direction);
    }

    public void Reset()
    {
        simulation.Reset();
        Paused = false;
        simulationTime = 0.0;
        accumulator = SimulationStep;
    }

    public TimelineValue Simulate()
    {
        if (Paused)
        {
            simulationReady = simulation.Step(simulationTime, SimulationStep, true, SimulationSettings);
        }
        else if (accumulator >= SimulationStep)
        {
            simulationTime += SimulationStep;
            accumulator -= SimulationStep;
            simulationReady = simulation.Step(simulationTime, SimulationStep, false, SimulationSettings);
        }

        frame.Time = (float)simulationTime;
        frame.InterpolationAlpha = Paused ? 1.0f : (float)Math.Clamp(accumulator / SimulationStep, 0.0, 1.0);

        return simulationReady;
    }

    public void Render(CommandBuffer commandBuffer)
    {
        ParticleData particles = new()
        {
            Particles = simulation.Particles,
            PreviousPositions = simulation.PreviousPositions,
            Count = simulation.ParticleCount,
            Radius = Simulation.ParticleRadius,
            Spacing = Simulation.ParticleSpacing,
            Minimum = simulation.Minimum,
            Maximum = simulation.Maximum,
            Version = simulationReady.Value
        };

        scenePass.Render(commandBuffer, frame, scene);
        glassPass.Render(commandBuffer, frame, scene, scenePass.Color, scenePass.DepthStencil, false);

        if (Settings.ViewMode is FluidViewMode.Water)
        {
            surfacePass.Render(commandBuffer, frame, particles, scenePass.LinearDepth);
        }

        waterPass.Render(commandBuffer, frame, Settings, scene, scenePass.Color, scenePass.LinearDepth, surfacePass.Output);

        if (Settings.ViewMode is FluidViewMode.Particles)
        {
            surfacePass.RenderParticles(commandBuffer, frame, particles, waterPass.Color, scenePass.DepthStencil);
        }

        glassPass.Render(commandBuffer, frame, scene, waterPass.Color, scenePass.DepthStencil, true);
        outputPass.Render(commandBuffer, waterPass.Color, Color, Settings.Exposure, Settings.AntialiasingEnabled);
    }

    public void Resize(uint width, uint height)
    {
        if (Color is not null && Color.Desc.Width == width && Color.Desc.Height == height)
        {
            return;
        }

        Color?.Dispose();
        Color = GraphicsHelper.CreateTexture(context, PixelFormat.B8G8R8A8UNorm, width, height, TextureUsages.ColorAttachment | TextureUsages.Sampled);
        scenePass.Resize(width, height);
        outputPass.Resize(width, height);
        ResizeSurface();
    }

    public void Dispose()
    {
        outputPass.Dispose();
        glassPass.Dispose();
        waterPass.Dispose();
        surfacePass.Dispose();
        scenePass.Dispose();
        scene.Dispose();
        simulation.Dispose();
        Color.Dispose();
    }

    private void ResizeSurface()
    {
        uint width = Math.Max((uint)(Color.Desc.Width * Settings.SurfaceScale), 1u);
        uint height = Math.Max((uint)(Color.Desc.Height * Settings.SurfaceScale), 1u);

        surfacePass.Resize(width, height);
        waterPass.Resize(Color.Desc.Width, Color.Desc.Height, width, height);
    }
}
