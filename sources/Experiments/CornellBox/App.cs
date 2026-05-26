using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Helpers;
using CornellBox.Renderers;
using Hexa.NET.ImGui;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace CornellBox;

internal static class App
{
    private static readonly IWindow window;
    private static readonly IInputContext input;
    private static readonly SwapChain swapChain;
    private static readonly ImGuiHandler imGui;
    private static readonly CameraHandler camera;
    private static readonly PathTracingRenderer? pathTracer;
    private static readonly RasterizationRenderer rasterizer;

    private static Renderer activeRenderer;
    private static int currentMode;

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
            Title = "Cornell Box - Zenith.NET",
            Size = new(1280, 720)
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

        swapChain = Context.CreateSwapChain(new() { Surface = surface, ColorFormat = PixelFormat.B8G8R8A8UNorm, DepthStencilFormat = PixelFormat.D32FloatS8UInt });
        imGui = new(input, new() { ColorFormats = [PixelFormat.B8G8R8A8UNorm], DepthStencilFormat = PixelFormat.D32FloatS8UInt });
        camera = new(input, Matrix4x4.CreateTranslation(278.0f, 273.0f, -800.0f))
        {
            Speed = 240.0f,
            FarPlane = 2000.0f
        };

        rasterizer = new();

        if (Context.Capabilities.RayTracing)
        {
            activeRenderer = pathTracer = new();
            currentMode = 0;
        }
        else
        {
            activeRenderer = rasterizer;
            currentMode = 1;
        }
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
            camera.Update(delta, width, height);

            // ImGui
            {
                ImGui.GetBackgroundDrawList().AddImage(imGui.Binding(activeRenderer.Color), new(0, 0), new(Width / DpiScale.X, Height / DpiScale.Y));

                ImGui.SetNextWindowPos(new(10, 10), ImGuiCond.FirstUseEver);
                if (ImGui.Begin("Cornell Box", ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text($"GraphicsApi: {Context.GraphicsApi}");
                    ImGui.Text(Context.Capabilities.DeviceName);

                    ImGui.Separator();

                    ImGui.Text("Render Mode:");

                    if (Context.Capabilities.RayTracing)
                    {
                        if (ImGui.RadioButton("Path Tracing", currentMode is 0) && currentMode is not 0)
                        {
                            pathTracer!.FrameCount = 0;

                            currentMode = 0;
                            activeRenderer = pathTracer;
                        }

                        ImGui.SameLine();
                    }

                    if (ImGui.RadioButton("Rasterization", currentMode is 1) && currentMode is not 1)
                    {
                        currentMode = 1;
                        activeRenderer = rasterizer;
                    }

                    ImGui.Separator();

                    if (currentMode is 0 && pathTracer is not null)
                    {
                        ImGui.Text($"SPP: {pathTracer.FrameCount}");
                    }

                    ImGui.Text($"FPS: {ImGui.GetIO().Framerate:F1}");
                }
                ImGui.End();
            }

            activeRenderer.Update(camera);
        };

        window.Render += _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            CommandBuffer commandBuffer = Context.GraphicsQueue.AcquireCommandBuffer();

            activeRenderer.Render(commandBuffer);

            imGui.Render(commandBuffer, new()
            {
                Texture = swapChain.CurrentTexture,
                LoadOp = LoadOp.Load,
                StoreOp = StoreOp.Store
            });

            swapChain.Present(commandBuffer.Submit()).Wait();
        };

        window.Resize += _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            pathTracer?.Resize(Width, Height);
            rasterizer.Resize(Width, Height);
            swapChain.Resize(Width, Height);
        };

        window.Run();

        pathTracer?.Dispose();
        rasterizer.Dispose();
        imGui.Dispose();
        swapChain.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();

        Console.WriteLine("Exited cleanly.");
    }
}
