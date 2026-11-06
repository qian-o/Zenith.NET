using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Helpers;
using CornellBox.Models;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Zenith.NET;
using Zenith.NET.Extensions.ImGui;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox;

internal static class App
{
    public const PixelFormat ColorFormat = PixelFormat.B8G8R8A8UNorm;

    private static readonly IWindow window;
    private static readonly IInputContext input;
    private static readonly ImGuiHandler imGui;
    private static readonly CameraHandler camera;
    private static readonly UserInterface userInterface;
    private static readonly Renderer renderer;
    private static readonly Presentation presentation;

    static App()
    {
        Context = PlatformHelper.CreateContext();
        Context.InitializeUpscaling([UpscalingApi.DLSS, UpscalingApi.FSR, UpscalingApi.MetalFX]);
        Context.ValidationMessage += static (_, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");

        window = CreateWindow();
        input = window.CreateInput();
        imGui = new(input);

        Context.InitializeImGui(imGui, new()
        {
            ColorFormats = [ColorFormat],
            SampleCount = SampleCount.Count1
        }, ImGuiColorSpace.Legacy);

        camera = new(input, new(278.0f, 720.0f, -1900.0f), new(278.0f, 180.0f, 240.0f))
        {
            FarPlane = 8000.0f,
            Speed = 240.0f
        };

        userInterface = new();
        renderer = new(userInterface.RenderSettings);
        presentation = new(PlatformHelper.CreateSurface(window), renderer.InputSize);
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

        window.Run();

        window.FramebufferResize -= OnFramebufferResize;
        window.Render -= OnRender;
        window.Update -= OnUpdate;

        presentation.Dispose();
        renderer.Dispose();
        camera.Dispose();
        imGui.Dispose();
        input.Dispose();
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
        userInterface.Update(deltaSeconds);

        renderer.ApplySettings(userInterface.RenderSettings);
        presentation.Configure(renderer.InputSize, userInterface.IsFrameGenerationEnabled);
    }

    private static void OnRender(double deltaSeconds)
    {
        if (PhysicalWidth is 0 || PhysicalHeight is 0)
        {
            return;
        }

        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();
        RenderedFrame frame = renderer.Render(commandBuffer, camera, deltaSeconds);

        presentation.Present(commandBuffer, frame);
    }

    private static void OnFramebufferResize(Vector2D<int> size)
    {
        if (PhysicalWidth is 0 || PhysicalHeight is 0)
        {
            return;
        }

        renderer.Resize();
        presentation.Configure(renderer.InputSize, userInterface.IsFrameGenerationEnabled);
    }

    private static IWindow CreateWindow()
    {
        IWindow window = Window.Create(WindowOptions.Default with
        {
            Size = new(1280, 720),
            API = GraphicsAPI.None,
            Title = "Cornell Box - Zenith.NET"
        });
        window.Initialize();
        window.Center();

        return window;
    }
}
