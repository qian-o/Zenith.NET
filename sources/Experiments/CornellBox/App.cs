using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Helpers;
using Hexa.NET.ImGui;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Extensions.ImGui;
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

        Context.ValidationMessage += static (_, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");

        window = Window.Create(WindowOptions.Default with
        {
            Size = new(1280, 720),
            API = GraphicsAPI.None,
            Title = "Cornell Box - Zenith.NET"
        });
        window.Initialize();
        window.Center();

        input = window.CreateInput();
        imGui = new(input);

        Context.InitializeImGui(imGui, new()
        {
            ColorFormats = [PixelFormat.B8G8R8A8UNorm],
            SampleCount = SampleCount.Count1
        }, ImGuiColorSpace.Legacy);

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

        camera = new(input, new(278.0f, 273.0f, -800.0f), new(278.0f, 273.0f, 0.0f))
        {
            FarPlane = 2000.0f,
            Speed = 240.0f
        };

        renderer = new();
    }

    public static GraphicsContext Context { get; }

    public static uint Width => (uint)window.FramebufferSize.X;

    public static uint Height => (uint)window.FramebufferSize.Y;

    public static Vector2 DpiScale => (Vector2)window.FramebufferSize / (Vector2)window.Size;

    public static void Run()
    {
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.Resize += OnResize;

        window.Run();

        window.Update -= OnUpdate;
        window.Render -= OnRender;
        window.Resize -= OnResize;

        renderer.Dispose();
        camera.Dispose();
        swapChain.Dispose();
        imGui.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();
    }

    private static void OnUpdate(double deltaSeconds)
    {
        if (Width is 0 || Height is 0)
        {
            return;
        }

        Vector2 dpiScale = DpiScale;
        uint width = (uint)(Width / dpiScale.X);
        uint height = (uint)(Height / dpiScale.Y);

        camera.Update(deltaSeconds, width, height);
        renderer.Update(deltaSeconds);

        ImGui.GetIO().DisplayFramebufferScale = dpiScale;
        Context.BeginImGuiFrame(deltaSeconds, width, height);

        ImGuiHelper.Overlay(DrawOverlay);
        ImGuiHelper.Settings(DrawSettings);

        ImGui.GetBackgroundDrawList().AddImage(renderer.Color.ImGuiBinding, Vector2.Zero, new(width, height));
    }

    private static void OnRender(double deltaSeconds)
    {
        if (Width is 0 || Height is 0)
        {
            return;
        }

        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        renderer.Render(commandBuffer, camera);

        Texture drawable = swapChain.Drawable;
        commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        Context.EndImGuiFrame(commandBuffer, ColorAttachment.Clear(drawable, default));
        commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);
        commandBuffer.Submit().Wait();

        swapChain.Present();
    }

    private static void OnResize(Vector2D<int> size)
    {
        if (Width is 0 || Height is 0)
        {
            return;
        }

        renderer.Resize(Width, Height);
        swapChain.Resize(Width, Height);
    }

    private static void DrawOverlay()
    {
        ImGui.Text(Context.Capabilities.DeviceName);
        ImGui.Text($"GraphicsApi: {Context.GraphicsApi}");
        ImGui.Text($"FPS: {ImGui.GetIO().Framerate:F1}");
    }

    private static void DrawSettings()
    {
        ImGui.SeparatorText("Scene");

        bool isPaused = renderer.IsPaused;
        if (ImGui.Checkbox("Pause", ref isPaused))
        {
            renderer.IsPaused = isPaused;
        }
    }
}
