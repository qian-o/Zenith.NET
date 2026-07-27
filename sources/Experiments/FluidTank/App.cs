using System.Numerics;
using FluidTank.Handlers;
using FluidTank.Helpers;
using FluidTank.Renderers;
using Hexa.NET.ImGui;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace FluidTank;

internal static class App
{
    private static readonly IWindow window;
    private static readonly IInputContext input;
    private static readonly SwapChain swapChain;
    private static readonly ImGuiHandler imGui;
    private static readonly CameraHandler camera;
    private static readonly FluidTankRenderer renderer;

    static App()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("This application only supports Windows, macOS, and Linux.");
        }

        if (OperatingSystem.IsWindows())
        {
            Context = GraphicsContext.CreateDirectX12(useValidationLayer: true);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Context = GraphicsContext.CreateMetal(useValidationLayer: true);
        }
        else
        {
            Context = GraphicsContext.CreateVulkan(useValidationLayer: true);
        }

        Context.ValidationMessage += static (sender, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");

        window = Window.Create(WindowOptions.Default with
        {
            API = GraphicsAPI.None,
            Title = "Fluid Tank - Zenith.NET",
            Size = new(1440, 900)
        });
        window.Initialize();

        input = window.CreateInput();

        Surface surface;
        if (OperatingSystem.IsWindows())
        {
            surface = Surface.Win32(window.Native!.Win32!.Value.Hwnd, Width, Height);
        }
        else if (OperatingSystem.IsMacOS())
        {
            surface = Surface.Apple(CocoaHelper.CreateLayer(window.Native!.Cocoa!.Value), Width, Height);
        }
        else
        {
            if (window.Native?.X11 is not { } x11)
            {
                throw new PlatformNotSupportedException("FluidTank requires an X11 or XWayland window on Linux.");
            }

            surface = Surface.Xlib(x11.Display, (nint)x11.Window, Width, Height);
        }

        swapChain = Context.CreateSwapChain(new()
        {
            Surface = surface,
            Format = PixelFormat.B8G8R8A8UNorm
        });

        imGui = new(input, new()
        {
            ColorFormats = [PixelFormat.B8G8R8A8UNorm],
            SampleCount = SampleCount.Count1
        });

        camera = new(input, new(9.2f, 5.3f, -10.8f), new(0.0f, 1.45f, 0.0f))
        {
            Speed = 4.0f,
            NearPlane = 0.05f,
            FarPlane = 80.0f,
            Fov = 48.0f
        };

        renderer = new();
    }

    public static GraphicsContext Context { get; }

    public static uint Width => (uint)window.FramebufferSize.X;

    public static uint Height => (uint)window.FramebufferSize.Y;

    public static Vector2 DpiScale => (Vector2)window.FramebufferSize / (Vector2)window.Size;

    public static void Run()
    {
        window.Update += delta =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            uint width = (uint)(Width / DpiScale.X);
            uint height = (uint)(Height / DpiScale.Y);

            imGui.Update(delta, width, height);
            camera.Update(delta, width, height, !ImGui.GetIO().WantCaptureKeyboard);
            renderer.Update(camera, delta);

            if (camera.TryConsumeClickRay(out Vector3 origin, out Vector3 direction) && !ImGui.GetIO().WantCaptureMouse)
            {
                renderer.PushFluid(origin, direction);
            }

            ImGui.GetBackgroundDrawList().AddImage(imGui.Binding(renderer.Color), new(0, 0), new(Width / DpiScale.X, Height / DpiScale.Y));

            DrawControlPanel();
        };

        window.Render += _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            TimelineValue simulationReady = renderer.Simulate();

            CommandBuffer sceneCommandBuffer = Context.GraphicsQueue.CommandBuffer();
            renderer.RenderScene(sceneCommandBuffer);
            sceneCommandBuffer.Submit();

            CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();
            renderer.RenderFluid(commandBuffer);

            commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
            imGui.Render(commandBuffer, ColorAttachment.DontCare(swapChain.Drawable));
            commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);

            commandBuffer.Submit(simulationReady).Wait();

            swapChain.Present();
        };

        window.Resize += _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            renderer.Resize(Width, Height);
            swapChain.Resize(Width, Height);
        };

        window.Run();

        renderer.Dispose();
        imGui.Dispose();
        swapChain.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();
    }

    private static void DrawControlPanel()
    {
        ImGui.SetNextWindowPos(new(10, 10), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Fluid Tank", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text($"GraphicsApi: {Context.GraphicsApi}");
            ImGui.Text(Context.Capabilities.DeviceName);
            ImGui.Text($"Particles: {renderer.ParticleCount:N0}");
            ImGui.Text($"FPS: {ImGui.GetIO().Framerate:F1}");

            ImGui.Separator();
            ImGui.Text("Simulation:");

            bool paused = renderer.Paused;
            if (ImGui.Checkbox("Pause", ref paused))
            {
                renderer.Paused = paused;
            }

            ImGui.SameLine();
            if (ImGui.Button("Reset dam"))
            {
                renderer.Reset();
            }

            bool waveMakerEnabled = renderer.WaveMakerEnabled;
            if (ImGui.Checkbox("Wave maker", ref waveMakerEnabled))
            {
                renderer.WaveMakerEnabled = waveMakerEnabled;
            }

            float waveAmplitude = renderer.WaveAmplitude;
            if (ImGui.SliderFloat("Wave amplitude", ref waveAmplitude, 0.0f, 0.34f, "%.2f m"))
            {
                renderer.WaveAmplitude = waveAmplitude;
            }

            float waveFrequency = renderer.WaveFrequency;
            if (ImGui.SliderFloat("Wave frequency", ref waveFrequency, 0.2f, 2.5f, "%.2f Hz"))
            {
                renderer.WaveFrequency = waveFrequency;
            }

            float viscosity = renderer.Viscosity;
            if (ImGui.SliderFloat("Viscosity", ref viscosity, 0.0f, 0.12f, "%.3f"))
            {
                renderer.Viscosity = viscosity;
            }

            int solverIterations = renderer.SolverIterations;
            if (ImGui.SliderInt("PBF iterations", ref solverIterations, 2, 6))
            {
                renderer.SolverIterations = solverIterations;
            }

            float surfaceTension = renderer.SurfaceTension;
            if (ImGui.SliderFloat("Surface tension", ref surfaceTension, 0.0f, 0.08f, "%.3f"))
            {
                renderer.SurfaceTension = surfaceTension;
            }

            ImGui.Separator();
            ImGui.Text("View:");

            DrawViewMode("Water", FluidViewMode.Water);
            ImGui.SameLine();
            DrawViewMode("Particles", FluidViewMode.Particles);

            if (renderer.ViewMode is FluidViewMode.Particles)
            {
                ImGui.Text("Color: Speed");
            }
            else
            {
                ImGui.Separator();
                ImGui.Text("Water:");

                float clarity = renderer.Clarity;
                if (ImGui.SliderFloat("Clarity", ref clarity, 0.25f, 2.0f, "%.2f"))
                {
                    renderer.Clarity = clarity;
                }

                float refraction = renderer.RefractionStrength;
                if (ImGui.SliderFloat("Refraction", ref refraction, 0.0f, 1.5f, "%.2f"))
                {
                    renderer.RefractionStrength = refraction;
                }
            }

            ImGui.Separator();
            ImGui.Text($"Ray Tracing: {(renderer.RayTracingEnabled ? "Enabled" : "Disabled")}");
        }

        ImGui.End();
    }

    private static void DrawViewMode(string label, FluidViewMode mode)
    {
        if (ImGui.RadioButton(label, renderer.ViewMode == mode))
        {
            renderer.ViewMode = mode;
        }
    }
}