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
    public const PixelFormat ColorFormat = PixelFormat.B8G8R8A8UNorm;

    private static readonly IWindow window;
    private static readonly IInputContext input;
    private static readonly ImGuiHandler imGui;
    private static readonly CameraHandler camera;
    private static readonly Renderer renderer;

    private static SwapChain? swapChain;
    private static FrameGenerationSwapChain? frameGenerationSwapChain;
    private static bool isFrameGenerationEnabled;
    private static bool isVerticalSyncEnabled;

    static App()
    {
        if (OperatingSystem.IsWindows())
        {
            Context = GraphicsContext.CreateDirectX12(useValidationLayer: true);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Context = GraphicsContext.CreateMetal(useValidationLayer: true);
        }
        else if (OperatingSystem.IsLinux())
        {
            Context = GraphicsContext.CreateVulkan(useValidationLayer: true);
        }
        else
        {
            throw new PlatformNotSupportedException("This application only supports Windows, macOS, and Linux.");
        }

        if (!Context.Capabilities.RayTracingSupported)
        {
            Context.Dispose();

            throw new PlatformNotSupportedException($"Selected GPU '{Context.Capabilities.DeviceName}' does not support hardware ray tracing.");
        }

        Context.ValidationMessage += static (_, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");

        Context.InitializeUpscaling();

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
            ColorFormats = [ColorFormat],
            SampleCount = SampleCount.Count1
        }, ImGuiColorSpace.Legacy);

        isFrameGenerationEnabled = Context.UpscalingCapabilities.FrameGenerationSupported;
        CreateSwapChain();

        camera = new(input, new(0.0f, 244.0f, -935.0f), new(0.0f, 210.0f, 255.0f))
        {
            NearPlane = 8.0f,
            FarPlane = 8000.0f,
            Speed = 280.0f
        };

        renderer = new();
    }

    public static GraphicsContext Context { get; }

    public static uint LogicalWidth => (uint)window.Size.X;

    public static uint LogicalHeight => (uint)window.Size.Y;

    public static uint PhysicalWidth => (uint)window.FramebufferSize.X;

    public static uint PhysicalHeight => (uint)window.FramebufferSize.Y;

    public static Vector2 DpiScale => (Vector2)window.FramebufferSize / (Vector2)window.Size;

    public static void Run()
    {
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.FramebufferResize += OnFramebufferResize;
        window.Closing += OnClosing;

        window.Run();

        window.Dispose();
        Context.Dispose();
    }

    private static void OnUpdate(double deltaSeconds)
    {
        if (PhysicalWidth is 0 || PhysicalHeight is 0)
        {
            return;
        }

        camera.Update(deltaSeconds);
        renderer.Update(deltaSeconds);

        Context.BeginImGuiFrame(deltaSeconds, LogicalWidth, LogicalHeight, DpiScale);

        ImGuiHelper.Overlay(DrawOverlay);
        ImGuiHelper.Settings(DrawSettings);

        ImGui.GetBackgroundDrawList().AddImage(renderer.Color.ImGuiBinding, Vector2.Zero, new(LogicalWidth, LogicalHeight));
    }

    private static void OnRender(double deltaSeconds)
    {
        if (PhysicalWidth is 0 || PhysicalHeight is 0)
        {
            return;
        }

        Context.GraphicsQueue.Timeline.Signal().Wait();

        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        FrameGenerationSwapChainArgs args = renderer.Render(commandBuffer, camera, deltaSeconds);

        Texture drawable = frameGenerationSwapChain?.Drawable ?? swapChain!.Drawable;
        commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        Context.EndImGuiFrame(commandBuffer, ColorAttachment.Clear(drawable, default));
        commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);

        if (frameGenerationSwapChain is not null)
        {
            commandBuffer.Submit();
            frameGenerationSwapChain.Present(args);
        }
        else
        {
            commandBuffer.Submit().Wait();
            swapChain!.Present();
        }
    }

    private static void OnFramebufferResize(Vector2D<int> size)
    {
        if (PhysicalWidth is 0 || PhysicalHeight is 0)
        {
            return;
        }

        frameGenerationSwapChain?.Resize(PhysicalWidth, PhysicalHeight);

        Context.GraphicsQueue.Timeline.Signal().Wait();

        swapChain?.Resize(PhysicalWidth, PhysicalHeight);
        renderer.Resize();
    }

    private static void OnClosing()
    {
        window.Closing -= OnClosing;
        window.FramebufferResize -= OnFramebufferResize;
        window.Render -= OnRender;
        window.Update -= OnUpdate;

        DestroySwapChain();
        renderer.Dispose();
        camera.Dispose();
        imGui.Dispose();
        input.Dispose();
    }

    private static void DrawOverlay()
    {
        float framerate = ImGui.GetIO().Framerate * (frameGenerationSwapChain is not null ? 2.0f : 1.0f);

        ImGui.Text(Context.Capabilities.DeviceName);
        ImGui.Text($"GraphicsApi: {Context.GraphicsApi}");
        ImGui.Text($"FPS: {framerate:F1}");
    }

    private static void DrawSettings()
    {
        bool isPaused = renderer.IsPaused;
        if (ImGui.Checkbox("Pause", ref isPaused))
        {
            renderer.IsPaused = isPaused;
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Reset").X - (ImGui.GetStyle().FramePadding.X * 2.0f));

        if (ImGui.Button("Reset"))
        {
            camera.Reset();
            renderer.Reset();
        }

        bool verticalSyncChanged = ImGui.Checkbox("VSync", ref isVerticalSyncEnabled);

        ImGui.Separator();

        bool renderChanged = false;

        ImGui.BeginDisabled(!Context.UpscalingCapabilities.SuperResolutionSupported);

        int qualityIndex = renderer.Quality is UpscalingQuality quality ? (int)quality + 1 : 0;
        if (ImGui.Combo("Upscaling Quality", ref qualityIndex, "Off\0Native\0Quality\0Balanced\0Performance\0Ultra Performance\0"))
        {
            renderer.Quality = qualityIndex is 0 ? null : (UpscalingQuality)(qualityIndex - 1);

            if (renderer.Quality is null)
            {
                renderer.IsRayReconstructionEnabled = false;
            }

            renderChanged = true;
        }

        ImGui.EndDisabled();

        ImGui.BeginDisabled(!Context.UpscalingCapabilities.RayReconstructionSupported || renderer.Quality is null);

        bool isRayReconstructionEnabled = renderer.IsRayReconstructionEnabled;
        if (ImGui.Checkbox("Ray Reconstruction", ref isRayReconstructionEnabled))
        {
            renderer.IsRayReconstructionEnabled = isRayReconstructionEnabled;
            renderChanged = true;
        }

        ImGui.EndDisabled();

        ImGui.BeginDisabled(!Context.UpscalingCapabilities.FrameGenerationSupported);

        bool frameGenerationChanged = ImGui.Checkbox("Frame Generation", ref isFrameGenerationEnabled);

        ImGui.EndDisabled();

        if (verticalSyncChanged || frameGenerationChanged)
        {
            DestroySwapChain();
            CreateSwapChain();
        }

        if (renderChanged)
        {
            Context.GraphicsQueue.Timeline.Signal().Wait();

            renderer.Resize();
        }
    }

    private static Surface CreateSurface()
    {
        if (OperatingSystem.IsWindows())
        {
            return Surface.Win32(window.Native!.Win32!.Value.Hwnd, PhysicalWidth, PhysicalHeight);
        }

        if (OperatingSystem.IsMacOS())
        {
            return Surface.Apple(CocoaHelper.CreateLayer(window.Native!.Cocoa!.Value), PhysicalWidth, PhysicalHeight);
        }

        if (window.Native!.Wayland.HasValue)
        {
            return Surface.Wayland(window.Native.Wayland.Value.Display, window.Native.Wayland.Value.Surface, PhysicalWidth, PhysicalHeight);
        }

        return Surface.Xlib(window.Native.X11!.Value.Display, (nint)window.Native.X11.Value.Window, PhysicalWidth, PhysicalHeight);
    }

    private static void CreateSwapChain()
    {
        Surface surface = CreateSurface();

        if (isFrameGenerationEnabled)
        {
            frameGenerationSwapChain = Context.CreateFrameGenerationSwapChain(new()
            {
                Surface = surface,
                Format = ColorFormat,
                IsVerticalSyncEnabled = isVerticalSyncEnabled
            });
        }
        else
        {
            swapChain = Context.CreateSwapChain(new()
            {
                Surface = surface,
                Format = ColorFormat,
                IsVerticalSyncEnabled = isVerticalSyncEnabled
            });
        }
    }

    private static void DestroySwapChain()
    {
        frameGenerationSwapChain?.Dispose();
        frameGenerationSwapChain = null;

        Context.GraphicsQueue.Timeline.Signal().Wait();

        swapChain?.Dispose();
        swapChain = null;
    }
}
