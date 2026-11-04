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
using Zenith.NET.Extensions.Upscaling;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace CornellBox;

internal static class App
{
    private static readonly IWindow window;
    private static readonly IInputContext input;
    private static readonly ImGuiHandler imGui;
    private static readonly FramePresenter presenter;
    private static readonly CameraHandler camera;
    private static readonly Renderer renderer;
    private static readonly Lock sizeLock = new();

    private static Vector2D<int> pendingLogicalSize;
    private static Vector2D<int> logicalSize;

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

        Context.InitializeUpscaling([UpscalingApi.DLSS, UpscalingApi.MetalFX]);

        Context.ValidationMessage += static (_, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");

        window = Window.Create(WindowOptions.Default with
        {
            IsEventDriven = true,
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

        pendingLogicalSize = window.Size;
        logicalSize = pendingLogicalSize;

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

    public static uint LogicalWidth => (uint)logicalSize.X;

    public static uint LogicalHeight => (uint)logicalSize.Y;

    public static uint PhysicalWidth => presenter.Width;

    public static uint PhysicalHeight => presenter.Height;

    public static Vector2 DpiScale => new Vector2(PhysicalWidth, PhysicalHeight) / (Vector2)logicalSize;

    public static void Run()
    {
        presenter.Render += OnRender;
        presenter.Resize += renderer.Resize;

        window.Update += imGui.UpdateCursor;
        window.Resize += OnResize;
        window.FramebufferResize += OnFramebufferResize;
        window.Closing += OnClosing;

        presenter.Start();

        while (!window.IsClosing)
        {
            window.DoEvents();
        }

        window.Closing -= OnClosing;
        window.FramebufferResize -= OnFramebufferResize;
        window.Resize -= OnResize;
        window.Update -= imGui.UpdateCursor;

        presenter.Resize -= renderer.Resize;
        presenter.Render -= OnRender;

        renderer.Dispose();
        presenter.Dispose();
        imGui.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();
    }

    private static void OnRender(CommandBuffer commandBuffer, double deltaSeconds)
    {
        using (Lock.Scope _ = sizeLock.EnterScope())
        {
            logicalSize = pendingLogicalSize;
        }

        imGui.Update();
        Context.BeginImGuiFrame(deltaSeconds, LogicalWidth, LogicalHeight, DpiScale);

        camera.Update(deltaSeconds);

        ImGuiHelper.Overlay(DrawOverlay);
        ImGuiHelper.Settings(DrawSettings);

        commandBuffer.Transition(renderer.Overlay, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        Context.EndImGuiFrame(commandBuffer, ColorAttachment.Clear(renderer.Overlay, Vector4.Zero));
        commandBuffer.Transition(renderer.Overlay, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);

        foreach (Texture frame in renderer.Render(commandBuffer, camera, deltaSeconds))
        {
            presenter.Submit(commandBuffer, frame);
        }
    }

    private static void OnResize(Vector2D<int> size)
    {
        if (size.X is 0 || size.Y is 0)
        {
            return;
        }

        using Lock.Scope _ = sizeLock.EnterScope();

        pendingLogicalSize = size;
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

        ImGui.BeginDisabled(!Context.UpscalingCapabilities.TemporalUpscalerSupported);

        int upscalingMode = renderer.UpscalingMode is UpscalingMode mode ? (int)mode + 1 : 0;
        if (ImGui.Combo("Upscaling Mode", ref upscalingMode, "Off\0Native\0Quality\0Balanced\0Performance\0Ultra Performance\0"))
        {
            renderer.UpscalingMode = upscalingMode is 0 ? null : (UpscalingMode)(upscalingMode - 1);
        }

        ImGui.EndDisabled();

        ImGui.BeginDisabled(!Context.UpscalingCapabilities.DenoisingUpscalerSupported || renderer.UpscalingMode is null);

        bool isDenoisingEnabled = renderer.IsDenoisingEnabled;
        if (ImGui.Checkbox("Denoising", ref isDenoisingEnabled))
        {
            renderer.IsDenoisingEnabled = isDenoisingEnabled;
        }

        ImGui.EndDisabled();

        ImGui.BeginDisabled(!Context.UpscalingCapabilities.FrameGeneratorSupported);

        bool isFrameGenerationEnabled = renderer.IsFrameGenerationEnabled;
        if (ImGui.Checkbox("Frame Generation", ref isFrameGenerationEnabled))
        {
            renderer.IsFrameGenerationEnabled = isFrameGenerationEnabled;
        }

        ImGui.EndDisabled();
    }
}
