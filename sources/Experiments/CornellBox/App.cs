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
    private static IRenderer activeRenderer;
    private static int currentMode;

    static App()
    {
        if (OperatingSystem.IsWindows())
        {
            Context = GraphicsContext.CreateDirectX12(useValidationLayer: true);
        }
        else if (OperatingSystem.IsLinux())
        {
            Context = GraphicsContext.CreateVulkan(useValidationLayer: true);
        }
        else
        {
            Context = GraphicsContext.CreateMetal(useValidationLayer: true);
        }

        Context.ValidationMessage += static (sender, args) => Console.WriteLine($"[{args.Source} - {args.Severity}] {args.Message}");

        window = Window.Create(WindowOptions.Default with { API = GraphicsAPI.None });
        window.Size = new(1280, 720);
        window.Initialize();
        window.Center();

        input = window.CreateInput();

        Surface surface;
        if (OperatingSystem.IsWindows())
        {
            surface = Surface.Win32(window.Native!.Win32!.Value.Hwnd, Width, Height);
        }
        else if (OperatingSystem.IsLinux())
        {
            surface = Surface.Xlib(window.Native!.X11!.Value.Display, (nint)window.Native.X11.Value.Window, Width, Height);
        }
        else
        {
            surface = Surface.Apple(CocoaHelper.CreateLayer(window.Native!.Cocoa!.Value), Width, Height);
        }

        swapChain = Context.CreateSwapChain(new() { Surface = surface, ColorTargetFormat = PixelFormat.B8G8R8A8UNorm, DepthStencilTargetFormat = PixelFormat.D32FloatS8UInt });
        imGui = new(input, swapChain.FrameBuffer.Output);
        camera = new(input, Matrix4x4.CreateTranslation(278f, 273f, -800f));
        camera.Speed = 240.0f;
        camera.FarPlane = 2000.0f;

        rasterizer = new(swapChain.FrameBuffer.Output);

        if (Context.Capabilities.RayTracingSupported)
        {
            pathTracer = new PathTracingRenderer();
            activeRenderer = pathTracer;
            currentMode = 0;
        }
        else
        {
            activeRenderer = rasterizer;
            currentMode = 1;
        }
    }

    public static GraphicsContext Context { get; }

    public static SwapChain SwapChain => swapChain;

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

            activeRenderer.Update(camera);
        };

        window.Render += _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            ImGui.SetNextWindowPos(new(10, 10), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Cornell Box", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Backend: {Context.Backend}");
                ImGui.Text(Context.Capabilities.DeviceName);

                ImGui.Separator();

                ImGui.Text("Render Mode:");

                if (Context.Capabilities.RayTracingSupported)
                {
                    if (ImGui.RadioButton("Path Tracing", currentMode is 0))
                    {
                        if (currentMode is not 0)
                        {
                            currentMode = 0;
                            activeRenderer = pathTracer!;
                            pathTracer!.ResetAccumulation();
                        }
                    }

                    ImGui.SameLine();
                }

                if (ImGui.RadioButton("Rasterization", currentMode is 1))
                {
                    if (currentMode is not 1)
                    {
                        currentMode = 1;
                        activeRenderer = rasterizer;
                    }
                }

                ImGui.Separator();

                if (currentMode is 0 && pathTracer is not null)
                {
                    ImGui.Text($"SPP: {pathTracer.FrameCount}");
                }

                ImGui.Text($"FPS: {ImGui.GetIO().Framerate:F1}");

                ImGui.End();
            }

            CommandBuffer commandBuffer = Context.Graphics.CommandBuffer();

            commandBuffer.BeginRenderPass(swapChain.FrameBuffer, ClearValues.Default);
            commandBuffer.EndRenderPass();

            activeRenderer.Render(commandBuffer, swapChain.FrameBuffer);

            imGui.Render(commandBuffer, swapChain.FrameBuffer, ClearValues.None);

            commandBuffer.Submit(true);

            swapChain.Present();
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

    public static ImTextureRef Binding(Texture texture)
    {
        return imGui.Binding(texture);
    }

    public static ImTextureRef Binding(TextureView textureView)
    {
        return imGui.Binding(textureView);
    }
}
