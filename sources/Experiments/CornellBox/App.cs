using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Helpers;
using Hexa.NET.ImGui;
using Silk.NET.Input;
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
    private static readonly IInputContext input;
    private static readonly ImGuiHandler imGui;
    private static readonly CameraHandler camera;
    private static readonly Renderer renderer;
    private static readonly FramePresenter presenter;
    private static readonly string[] frameGenerationNames;

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

        if (!Context.Capabilities.RayTracingSupported)
        {
            throw new NotSupportedException("Cornell Box requires ray tracing support.");
        }

        window = Window.Create(WindowOptions.Default with
        {
            Size = new(1280, 720),
            API = GraphicsAPI.None,
            Title = "Cornell Box - Zenith.NET"
        });
        window.Initialize();
        window.Center();

        Context.InitializeImGui(imGui = new(input = window.CreateInput()), new()
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

        camera = new(input, Matrix4x4.CreateTranslation(278.0f, 273.0f, -800.0f))
        {
            FarPlane = 2000.0f,
            Speed = 240.0f
        };

        renderer = new();
        presenter = new(Context, surface, PixelFormat.B8G8R8A8UNorm);
        frameGenerationNames = ["Off", .. Enumerable.Range(2, (int)renderer.MaxGeneratedFrameCount).Select(static multiplier => $"{multiplier}x")];
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

        presenter.Dispose();
        renderer.Dispose();
        camera.Dispose();
        imGui.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();
    }

    private static void OnUpdate(double deltaSeconds)
    {
        if (Width is 0 || Height is 0)
        {
            renderer.ResetHistory();

            return;
        }

        uint width = (uint)(Width / DpiScale.X);
        uint height = (uint)(Height / DpiScale.Y);

        camera.Update(deltaSeconds, width, height);
        Context.BeginImGuiFrame(deltaSeconds, width, height);

        ImGuiHelper.Overlay(DrawOverlay);
        ImGuiHelper.Settings(DrawSettings);
    }

    private static void OnRender(double deltaSeconds)
    {
        if (Width is 0 || Height is 0)
        {
            return;
        }

        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();
        Texture ui = renderer.UI;

        commandBuffer.Transition(ui, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        Context.EndImGuiFrame(commandBuffer, ColorAttachment.Clear(ui, default));
        commandBuffer.Transition(ui, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);

        renderer.Render(commandBuffer, camera, deltaSeconds);

        TimelineValue ready = commandBuffer.Submit();
        presenter.Present(renderer.Color, renderer.GeneratedColors, renderer.DisableInterpolation, [ready]);
    }

    private static void OnResize(Vector2D<int> size)
    {
        if (Width is 0 || Height is 0)
        {
            renderer.ResetHistory();

            return;
        }

        presenter.Resize(Width, Height);

        renderer.Resize(Width, Height);
    }

    private static void DrawOverlay()
    {
        ImGui.Text(Context.Capabilities.DeviceName);
        ImGui.Text($"GraphicsApi: {Context.GraphicsApi}");
        ImGui.Text($"FPS: {presenter.Framerate:F1}");
    }

    private static void DrawSettings()
    {
        ImGui.SeparatorText("Scene");

        bool isPaused = renderer.IsPaused;
        if (ImGui.Checkbox("Pause", ref isPaused))
        {
            renderer.IsPaused = isPaused;
        }

        ImGui.SeparatorText("DLSS");

        float comboWidth = ImGui.CalcTextSize("Ultra Performance").X + ImGui.GetFrameHeight() + (ImGui.GetStyle().FramePadding.X * 2.0f);

        ImGui.BeginDisabled(!renderer.RayReconstructionSupported);

        string rayReconstructionName = renderer.RayReconstruction switch
        {
            DLSSMode.DLAA => "DLAA",
            DLSSMode.Quality => "Quality",
            DLSSMode.Balanced => "Balanced",
            DLSSMode.Performance => "Performance",
            DLSSMode.UltraPerformance => "Ultra Performance",
            _ => "Off"
        };

        ImGui.SetNextItemWidth(comboWidth);

        if (ImGui.BeginCombo("Ray Reconstruction", rayReconstructionName))
        {
            DrawRayReconstructionMode("Off", null);
            DrawRayReconstructionMode("DLAA", DLSSMode.DLAA);
            DrawRayReconstructionMode("Quality", DLSSMode.Quality);
            DrawRayReconstructionMode("Balanced", DLSSMode.Balanced);
            DrawRayReconstructionMode("Performance", DLSSMode.Performance);
            DrawRayReconstructionMode("Ultra Performance", DLSSMode.UltraPerformance);
            ImGui.EndCombo();
        }

        ImGui.EndDisabled();

        ImGui.BeginDisabled(!renderer.FrameGenerationSupported);

        ImGui.SetNextItemWidth(comboWidth);

        if (ImGui.BeginCombo("Frame Generation", frameGenerationNames[renderer.GeneratedFrameCount]))
        {
            for (int i = 0; i < frameGenerationNames.Length; i++)
            {
                DrawFrameGenerationMode(frameGenerationNames[i], (uint)i);
            }

            ImGui.EndCombo();
        }

        ImGui.EndDisabled();
    }

    private static void DrawRayReconstructionMode(string name, DLSSMode? mode)
    {
        bool isSelected = renderer.RayReconstruction == mode;

        if (ImGui.Selectable(name, isSelected))
        {
            renderer.SetRayReconstruction(mode);
        }

        if (isSelected)
        {
            ImGui.SetItemDefaultFocus();
        }
    }

    private static void DrawFrameGenerationMode(string name, uint generatedFrameCount)
    {
        bool isSelected = renderer.GeneratedFrameCount == generatedFrameCount;

        if (ImGui.Selectable(name, isSelected))
        {
            renderer.SetGeneratedFrameCount(generatedFrameCount);
        }

        if (isSelected)
        {
            ImGui.SetItemDefaultFocus();
        }
    }
}
