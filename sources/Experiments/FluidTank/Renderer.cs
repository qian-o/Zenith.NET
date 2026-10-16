using System.Numerics;
using FluidTank.Handlers;
using FluidTank.Models;
using FluidTank.Passes;
using Zenith.NET;

namespace FluidTank;

internal class Renderer : DisposableObject
{
    private const double SimulationStepSeconds = 1.0 / 30.0;

    private readonly Simulation simulation;
    private readonly SceneResources scene;
    private readonly ScenePass scenePass;
    private readonly SurfacePass surfacePass;
    private readonly WaterPass waterPass;
    private readonly ParticlePass particlePass;
    private readonly GlassPass glassPass;
    private readonly OutputPass outputPass;

    private double accumulatedSeconds = SimulationStepSeconds;

    public Renderer()
    {
        simulation = new();
        scene = new();
        scenePass = new(App.Width, App.Height);
        surfacePass = new(App.Width, App.Height);
        waterPass = new(App.Width, App.Height);
        particlePass = new();
        glassPass = new();
        outputPass = new(App.Width, App.Height);
    }

    public FluidViewMode ViewMode { get; set; }

    public bool IsPaused { get; set; }

    public Texture Color => outputPass.Color;

    public void Update(double deltaSeconds)
    {
        if (!IsPaused)
        {
            accumulatedSeconds = Math.Min(accumulatedSeconds + Math.Min(deltaSeconds, SimulationStepSeconds), SimulationStepSeconds * 2.0);
        }
    }

    public void PushFluid(Vector3 origin, Vector3 direction)
    {
        simulation.Push(origin, direction);
    }

    public void Reset()
    {
        simulation.Reset();
        IsPaused = false;
        accumulatedSeconds = SimulationStepSeconds;
    }

    public TimelineValue Simulate()
    {
        if (IsPaused)
        {
            simulation.Step(SimulationStepSeconds, true);
        }
        else if (accumulatedSeconds >= SimulationStepSeconds)
        {
            accumulatedSeconds -= SimulationStepSeconds;
            simulation.Step(SimulationStepSeconds, false);
        }

        return simulation.Ready;
    }

    public void Render(CommandBuffer commandBuffer, CameraHandler camera)
    {
        float interpolationAlpha = IsPaused ? 1.0f : (float)Math.Clamp(accumulatedSeconds / SimulationStepSeconds, 0.0, 1.0);

        scenePass.Record(commandBuffer, scene, camera);
        glassPass.Record(commandBuffer, scene, camera, scenePass.Color, scenePass.DepthStencil, useFrontFaces: false);

        Texture color = scenePass.Color;

        if (ViewMode is FluidViewMode.Water)
        {
            surfacePass.Record(commandBuffer, simulation, camera, scenePass.LinearDepth, interpolationAlpha);
            waterPass.Record(commandBuffer, scene, camera, color, scenePass.LinearDepth, surfacePass.Depth, surfacePass.Thickness, surfacePass.Normal);
            color = waterPass.Color;
        }
        else
        {
            particlePass.Record(commandBuffer, simulation, camera, color, scenePass.DepthStencil, interpolationAlpha);
        }

        glassPass.Record(commandBuffer, scene, camera, color, scenePass.DepthStencil, useFrontFaces: true);
        outputPass.Record(commandBuffer, color);
    }

    public void Resize(uint width, uint height)
    {
        if (Color.Desc.Width == width && Color.Desc.Height == height)
        {
            return;
        }

        scenePass.Resize(width, height);
        surfacePass.Resize(width, height);
        waterPass.Resize(width, height);
        outputPass.Resize(width, height);
    }

    protected override void Destroy()
    {
        outputPass?.Dispose();
        glassPass?.Dispose();
        particlePass?.Dispose();
        waterPass?.Dispose();
        surfacePass?.Dispose();
        scenePass?.Dispose();
        scene?.Dispose();
        simulation?.Dispose();
    }
}
