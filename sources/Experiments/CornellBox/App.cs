using CornellBox.Handlers;
using CornellBox.Helpers;
using CornellBox.Models;
using Hexa.NET.ImGui;
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
    private static readonly PlatformHandler platform;
    private static readonly ImGuiHandler imGui;
    private static readonly FramePresenter presenter;
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
            UpdatesPerSecond = 60.0,
            FramesPerSecond = 60.0,
            Title = "Cornell Box - Zenith.NET"
        });
        window.Initialize();
        window.Center();

        platform = new(window);
        imGui = new(platform);

        Context.InitializeImGui(imGui, new()
        {
            ColorFormats = [PixelFormat.B8G8R8A8UNorm],
            SampleCount = SampleCount.Count1
        }, ImGuiColorSpace.Legacy);

        uint width = (uint)window.FramebufferSize.X;
        uint height = (uint)window.FramebufferSize.Y;

        Surface surface;
        if (OperatingSystem.IsWindows())
        {
            surface = Surface.Win32(window.Native!.Win32!.Value.Hwnd, width, height);
        }
        else if (OperatingSystem.IsMacOS())
        {
            surface = Surface.Apple(CocoaHelper.CreateLayer(window.Native!.Cocoa!.Value), width, height);
        }
        else
        {
            surface = Surface.Xlib(window.Native!.X11!.Value.Display, (nint)window.Native.X11.Value.Window, width, height);
        }

        presenter = new(Context, new()
        {
            Surface = surface,
            Format = PixelFormat.B8G8R8A8UNorm,
            IsVerticalSyncEnabled = false,
            IsPresentQueueEnabled = true
        });

        camera = new(new(278.0f, 720.0f, -1900.0f), new(278.0f, 180.0f, 240.0f))
        {
            FarPlane = 8000.0f,
            Speed = 240.0f
        };

        renderer = new();
    }

    public static GraphicsContext Context { get; }

    public static uint Width => presenter.Width;

    public static uint Height => presenter.Height;

    public static void Run()
    {
        presenter.Update += OnUpdate;
        presenter.Render += OnRender;
        presenter.Resize += OnResize;
        window.FramebufferResize += OnFramebufferResize;

        presenter.Start();
        window.Run();
        presenter.Stop();

        window.FramebufferResize -= OnFramebufferResize;
        presenter.Resize -= OnResize;
        presenter.Render -= OnRender;
        presenter.Update -= OnUpdate;

        renderer.Dispose();
        presenter.Dispose();
        platform.Dispose();
        window.Dispose();

        Context.Dispose();
    }

    private static void OnUpdate(double deltaSeconds)
    {
        uint width = (uint)(Width / platform.DpiScale.X);
        uint height = (uint)(Height / platform.DpiScale.Y);
        ReadOnlySpan<Input> inputs = platform.CollectInput();

        camera.Update(deltaSeconds, width, height, inputs);
        imGui.Update(inputs);
        renderer.Update(deltaSeconds);

        Context.BeginImGuiFrame(deltaSeconds, width, height);

        ImGuiHelper.Overlay(DrawOverlay);
        ImGuiHelper.Settings(DrawSettings);
    }

    private static void OnRender(double deltaSeconds)
    {
        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        renderer.Render(commandBuffer, camera);

        commandBuffer.Transition(renderer.Color, default, TextureLayout.ColorAttachment, TextureLayout.ColorAttachment);
        Context.EndImGuiFrame(commandBuffer, ColorAttachment.Load(renderer.Color));
        commandBuffer.Submit();

        presenter.Submit(renderer.Color, TextureLayout.ColorAttachment);
    }

    private static void OnFramebufferResize(Vector2D<int> size)
    {
        presenter.RequestResize((uint)size.X, (uint)size.Y);
    }

    private static void OnResize(uint width, uint height)
    {
        renderer.Resize(width, height);
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
