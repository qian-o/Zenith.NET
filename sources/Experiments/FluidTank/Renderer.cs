using System.Numerics;
using FluidTank.Handlers;
using FluidTank.Models;
using FluidTank.Passes;
using Zenith.NET;

namespace FluidTank;

internal class Renderer : DisposableObject
{
    private const double SimulationStep = 1.0 / 30.0;

    private readonly Simulation simulation;
    private readonly SceneResources scene;
    private readonly ScenePass scenePass;
    private readonly SurfacePass surfacePass;
    private readonly WaterPass waterPass;
    private readonly GlassPass glassPass;
    private readonly OutputPass outputPass;

    private double simulationTime;
    private double accumulator = SimulationStep;
    private TimelineValue simulationReady;

    public Renderer()
    {
        simulation = new();
        scene = new();
        scenePass = new(App.Width, App.Height);
        surfacePass = new(App.Width, App.Height);
        waterPass = new(App.Width, App.Height);
        glassPass = new(App.Width, App.Height);
        outputPass = new(App.Width, App.Height);
    }

    public FluidViewMode ViewMode { get; set; }

    public bool Paused { get; set; }

    public bool WaveMakerEnabled { get; set; }

    public Texture Color => outputPass.Color;

    public void Update(double delta)
    {
        if (!Paused)
        {
            accumulator = Math.Min(accumulator + Math.Min(delta, SimulationStep), SimulationStep * 2.0);
        }
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
            simulationReady = simulation.Step(simulationTime, SimulationStep, true, WaveMakerEnabled);
        }
        else if (accumulator >= SimulationStep)
        {
            simulationTime += SimulationStep;
            accumulator -= SimulationStep;
            simulationReady = simulation.Step(simulationTime, SimulationStep, false, WaveMakerEnabled);
        }

        return simulationReady;
    }

    public void Render(CommandBuffer commandBuffer, CameraHandler camera)
    {
        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);

        PassArgs args = new()
        {
            View = view,
            Projection = projection,
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            CameraPosition = camera.Position,
            CameraRight = camera.Right,
            CameraUp = camera.Up,
            SunDirection = Vector3.Normalize(new(-0.38f, -0.83f, -0.42f)),
            LightIntensity = 2.7f,
            Time = (float)simulationTime,
            InterpolationAlpha = Paused ? 1.0f : (float)Math.Clamp(accumulator / SimulationStep, 0.0, 1.0),
            Particles = new()
            {
                Particles = simulation.Particles,
                PreviousPositions = simulation.PreviousPositions,
                Count = Simulation.ParticleCount,
                Radius = Simulation.ParticleRadius,
                Spacing = Simulation.ParticleSpacing,
                Minimum = Simulation.TankMin,
                Maximum = Simulation.TankMax
            },
            Scene = scene,
            ViewMode = ViewMode
        };

        scenePass.Record(commandBuffer, in args);

        args = args with
        {
            Color = scenePass.Color,
            SceneDepth = scenePass.LinearDepth,
            DepthStencil = scenePass.DepthStencil,
            FrontFaces = false
        };
        glassPass.Record(commandBuffer, in args);

        if (args.ViewMode is FluidViewMode.Water)
        {
            surfacePass.Record(commandBuffer, in args);
        }

        args = args with
        {
            FluidDepth = surfacePass.Depth,
            Thickness = surfacePass.Thickness,
            Normal = surfacePass.Normal
        };
        waterPass.Record(commandBuffer, in args);

        args = args with { Color = waterPass.Color };

        if (args.ViewMode is FluidViewMode.Particles)
        {
            surfacePass.Record(commandBuffer, in args);
        }

        args = args with { FrontFaces = true };
        glassPass.Record(commandBuffer, in args);
        outputPass.Record(commandBuffer, in args);
    }

    public void Resize(uint width, uint height)
    {
        if (outputPass.Width == width && outputPass.Height == height)
        {
            return;
        }

        scenePass.Resize(width, height);
        surfacePass.Resize(width, height);
        waterPass.Resize(width, height);
        glassPass.Resize(width, height);
        outputPass.Resize(width, height);
    }

    protected override void Destroy()
    {
        outputPass.Dispose();
        glassPass.Dispose();
        waterPass.Dispose();
        surfacePass.Dispose();
        scenePass.Dispose();
        scene.Dispose();
        simulation.Dispose();
    }
}
