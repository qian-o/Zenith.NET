using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Helpers;
using CornellBox.Models;
using Hexa.NET.ImGui;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Extensions.DLSS;
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

    private static readonly string frameGenerationItems = "1x\0";

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

        switch (Technology)
        {
            case RenderingTechnology.DLSS:
                {
                    Context.InitializeDLSS(Path.Combine(AppContext.BaseDirectory, "DLSS"));

                    frameGenerationItems = string.Concat(Enumerable.Range(1, (int)Context.DLSSCapabilities.MaxGeneratedFrameCount + 1).Select(static count => $"{count}x\0"));
                }
                break;

            case RenderingTechnology.MetalFX:
                {
                    // 后续新增 MetalFX 扩展后以便使用超分降噪、帧生成技术。
                    // Context.InitializeMetalFX();

                    frameGenerationItems = string.Concat(Enumerable.Range(1, 2).Select(static count => $"{count}x\0"));
                }
                break;
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

    public static RenderingTechnology Technology { get; } = OperatingSystem.IsWindows() || OperatingSystem.IsLinux() ? RenderingTechnology.DLSS : RenderingTechnology.MetalFX;

    public static uint Width => presenter.Width;

    public static uint Height => presenter.Height;

    public static void Run()
    {
        presenter.Update += OnUpdate;
        presenter.Render += OnRender;
        presenter.Resize += OnResize;

        window.FramebufferResize += OnFramebufferResize;
        window.Closing += OnClosing;

        presenter.Start();
        window.Run();

        window.Closing -= OnClosing;
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

        commandBuffer.Transition(renderer.Overlay, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        Context.EndImGuiFrame(commandBuffer, ColorAttachment.Clear(renderer.Overlay, Vector4.Zero));
        commandBuffer.Transition(renderer.Overlay, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);

        renderer.Composite(commandBuffer);

        foreach (int index in renderer.ReadOutputIndices(commandBuffer.Submit()))
        {
            presenter.Submit(renderer.Colors[index], TextureLayout.ColorAttachment);
        }
    }

    private static void OnResize(uint width, uint height)
    {
        renderer.Resize(width, height);
    }

    private static void OnFramebufferResize(Vector2D<int> size)
    {
        presenter.RequestResize((uint)size.X, (uint)size.Y);
    }

    private static void OnClosing()
    {
        presenter.Stop();
    }

    private static void DrawOverlay()
    {
        ImGui.Text(Context.Capabilities.DeviceName);
        ImGui.Text($"GraphicsApi: {Context.GraphicsApi}");
        ImGui.Text($"FPS: {presenter.FramesPerSecond:F1}");
    }

    private static void DrawSettings()
    {
        bool isPaused = renderer.IsPaused;
        if (ImGui.Checkbox("Pause", ref isPaused))
        {
            renderer.IsPaused = isPaused;
        }

        int denoisingUpscaling = (int)renderer.DenoisingUpscaling;
        if (ImGui.Combo("Denoising Upscaling", ref denoisingUpscaling, "Off\0Native\0Quality\0Balanced\0Performance\0Ultra Performance\0"))
        {
            renderer.DenoisingUpscaling = (DenoisingUpscalingMode)denoisingUpscaling;
        }

        int frameGeneration = (int)renderer.FrameGeneration;
        if (ImGui.Combo("Frame Generation", ref frameGeneration, frameGenerationItems))
        {
            renderer.FrameGeneration = (FrameGenerationMode)frameGeneration;
        }
    }
}
