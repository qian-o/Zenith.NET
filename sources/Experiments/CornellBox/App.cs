using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Helpers;
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
        camera = new(input, Matrix4x4.Identity);
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
        };

        window.Render += _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            ImGui.Overlay("Info", () =>
            {
                ImGui.Text($"Backend: {Context.Backend}");

                ImGui.Separator();

                ImGui.Text(Context.Capabilities.DeviceName);

                ImGui.Separator();

                ImGui.Text($"Ray Tracing Supported: {Context.Capabilities.RayTracingSupported}");

                ImGui.Separator();

                ImGui.Text($"Mesh Shading Supported: {Context.Capabilities.MeshShadingSupported}");

                ImGui.Separator();

                ImGui.Text($"Current FPS: {ImGui.GetIO().Framerate:F1}");
            });

            CommandBuffer commandBuffer = Context.Graphics.CommandBuffer();

            imGui.Render(commandBuffer, swapChain.FrameBuffer, ClearValues.Default);

            commandBuffer.Submit(true);

            swapChain.Present();
        };

        window.Resize += _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            swapChain.Resize(Width, Height);
        };

        window.Run();

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
