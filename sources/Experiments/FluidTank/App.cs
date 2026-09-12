using System.Numerics;
using FluidTank.Handlers;
using FluidTank.Helpers;
using FluidTank.Models;
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
    private static readonly Renderer renderer;

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
            Size = new(1280, 720),
            API = GraphicsAPI.None,
            Title = "Fluid Tank - Zenith.NET"
        });
        window.Initialize();
        window.Center();

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
            surface = Surface.Xlib(window.Native!.X11!.Value.Display, (nint)window.Native.X11.Value.Window, Width, Height);
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
            NearPlane = 0.05f,
            FarPlane = 80.0f,
            Fov = 48.0f,
            Speed = 4.0f
        };

        renderer = new(Context, Width, Height);
    }

    public static GraphicsContext Context { get; }

    public static uint Width => (uint)window.FramebufferSize.X;

    public static uint Height => (uint)window.FramebufferSize.Y;

    public static Vector2 DpiScale => (Vector2)window.FramebufferSize / (Vector2)window.Size;

    public static void Run()
    {
        window.Update += static delta =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            uint width = (uint)(Width / DpiScale.X);
            uint height = (uint)(Height / DpiScale.Y);

            imGui.Update(delta, width, height);
            camera.Update(delta, width, height);
            renderer.Update(camera, delta);

            if (camera.TryConsumeClickRay(out Vector3 origin, out Vector3 direction) && !ImGui.GetIO().WantCaptureMouse)
            {
                renderer.PushFluid(origin, direction);
            }

            ImGui.GetBackgroundDrawList().AddImage(imGui.Binding(renderer.Color), new(0, 0), new(Width / DpiScale.X, Height / DpiScale.Y));

            ImGuiHelper.Overlay(static () =>
            {
                ImGui.Text(Context.Capabilities.DeviceName);
                ImGui.Text($"GraphicsApi: {Context.GraphicsApi}");
                ImGui.Text($"FPS: {ImGui.GetIO().Framerate:F1}");
                ImGui.Text($"Particles: {renderer.ParticleCount:N0}");
            });

            ImGuiHelper.Settings(static () =>
            {
                ImGui.Text("Run");
                ImGui.Checkbox("Pause", ref renderer.Paused);
                ImGui.SameLine();

                if (ImGui.Button("Reset dam"))
                {
                    renderer.Reset();
                }

                ImGui.Separator();
                ImGui.Text("Motion");
                ImGui.Checkbox("Wave maker", ref renderer.SimulationSettings.WaveMakerEnabled);
                ImGui.BeginDisabled(!renderer.SimulationSettings.WaveMakerEnabled);
                ImGui.SliderFloat("Wave amplitude", ref renderer.SimulationSettings.WaveAmplitude, 0.0f, 0.34f, "%.2f m", ImGuiSliderFlags.AlwaysClamp);
                ImGui.SliderFloat("Wave frequency", ref renderer.SimulationSettings.WaveFrequency, 0.2f, 2.5f, "%.2f Hz", ImGuiSliderFlags.AlwaysClamp);
                ImGui.EndDisabled();

                ImGui.Separator();
                ImGui.Text("Display");

                if (ImGui.RadioButton("Water", renderer.Settings.ViewMode is FluidViewMode.Water))
                {
                    renderer.Settings.ViewMode = FluidViewMode.Water;
                }

                ImGui.SameLine();

                if (ImGui.RadioButton("Particles", renderer.Settings.ViewMode is FluidViewMode.Particles))
                {
                    renderer.Settings.ViewMode = FluidViewMode.Particles;
                }

                if (renderer.Settings.ViewMode is FluidViewMode.Water)
                {
                    ImGui.SliderFloat("Clarity", ref renderer.Settings.Clarity, 0.25f, 2.0f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
                    ImGui.SliderFloat("Refraction", ref renderer.Settings.RefractionStrength, 0.0f, 1.5f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
                }

                if (ImGui.CollapsingHeader("Rendering"))
                {
                    ImGui.SliderFloat("Surface scale", ref renderer.Settings.SurfaceScale, 0.33f, 1.0f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
                    ImGui.SliderFloat("Exposure", ref renderer.Settings.Exposure, 0.5f, 2.0f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
                    ImGui.Checkbox("Antialiasing", ref renderer.Settings.AntialiasingEnabled);
                    ImGui.BeginDisabled(!Context.Capabilities.RayTracingSupported);
                    ImGui.Checkbox("Ray-traced reflections", ref renderer.Settings.RayTracingEnabled);
                    ImGui.EndDisabled();
                }

                if (ImGui.CollapsingHeader("Advanced simulation"))
                {
                    ImGui.SliderFloat("FLIP ratio", ref renderer.SimulationSettings.FlipRatio, 0.0f, 1.0f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
                    ImGui.SliderInt("Pressure iterations", ref renderer.SimulationSettings.PressureIterations, 4, 32, "%d", ImGuiSliderFlags.AlwaysClamp);
                    ImGui.SliderFloat("Velocity damping", ref renderer.SimulationSettings.VelocityDamping, 0.97f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
                    ImGui.Text("Solver: APIC / FLIP");
                }
            });
        };

        window.Render += static _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            TimelineValue simulationReady = renderer.Simulate();

            CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

            renderer.Render(commandBuffer);

            commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
            imGui.Render(commandBuffer, ColorAttachment.Clear(swapChain.Drawable, default));
            commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);

            commandBuffer.Submit(simulationReady).Wait();

            swapChain.Present();
        };

        window.Resize += static _ =>
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
}
