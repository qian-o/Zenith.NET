using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Helpers;
using Hexa.NET.ImGui;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Extensions.DLSS;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace CornellBox;

internal static class App
{
    public const int SlotCount = 3;

    private static readonly IWindow window;
    private static readonly IInputContext input;
    private static readonly SwapChain swapChain;
    private static readonly FramePresenter presenter;
    private static readonly ImGuiHandler imGui;
    private static readonly CameraHandler camera;
    private static readonly Renderer renderer;
    private static readonly Texture[] uiTextures = new Texture[SlotCount];
    private static readonly Texture[] backBuffers = new Texture[SlotCount];
    private static readonly Texture[] generatedFrames = new Texture[SlotCount];

    private static int slot;

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

        swapChain = Context.CreateSwapChain(new()
        {
            Surface = surface,
            Format = PixelFormat.B8G8R8A8UNorm,
            UsePresentQueue = true
        });

        presenter = new(swapChain, SlotCount);

        imGui = new(input, new()
        {
            ColorFormats = [PixelFormat.B8G8R8A8UNorm],
            SampleCount = SampleCount.Count1
        });

        camera = new(input, Matrix4x4.CreateTranslation(278.0f, 273.0f, -800.0f))
        {
            FarPlane = 2000.0f,
            Speed = 240.0f
        };

        renderer = new();

        CreateTargets();
    }

    public static GraphicsContext Context { get; }

    public static uint Width => (uint)window.FramebufferSize.X;

    public static uint Height => (uint)window.FramebufferSize.Y;

    public static Vector2 DpiScale => (Vector2)window.FramebufferSize / (Vector2)window.Size;

    public static void Run()
    {
        window.Update += static delta =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            uint width = (uint)(Width / DpiScale.X);
            uint height = (uint)(Height / DpiScale.Y);

            imGui.Update(delta, width, height);
            camera.Update(delta, width, height);

            ImGuiHelper.Overlay(static () =>
            {
                ImGui.Text(Context.Capabilities.DeviceName);
                ImGui.Text($"GraphicsApi: {Context.GraphicsApi}");
                ImGui.Text($"FPS: {presenter.Framerate:F1}");
            });

            ImGuiHelper.Settings(Settings);
        };

        window.Render += static delta =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            presenter.Wait(slot);

            CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

            commandBuffer.Transition(uiTextures[slot], default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
            imGui.Render(commandBuffer, ColorAttachment.Clear(uiTextures[slot], default));
            commandBuffer.Transition(uiTextures[slot], default, TextureLayout.ColorAttachment, TextureLayout.Sampled);

            renderer.Render(commandBuffer, camera, delta, slot, uiTextures[slot], backBuffers[slot], generatedFrames[slot]);

            TimelineValue value = commandBuffer.Submit();
            TimelineValue presentable = renderer.GenerateFrame(value);

            value.Wait();

            presenter.Present(slot, backBuffers[slot], renderer.IsFrameGenerated ? generatedFrames[slot] : null, presentable);

            slot = (slot + 1) % SlotCount;
        };

        window.Resize += static _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            presenter.Drain();

            swapChain.Resize(Width, Height);

            DestroyTargets();
            CreateTargets();

            renderer.Resize(Width, Height);
        };

        window.Run();

        presenter.Dispose();

        DestroyTargets();

        renderer.Dispose();
        imGui.Dispose();
        swapChain.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();
    }

    private static void Settings()
    {
        bool paused = renderer.Paused;

        if (ImGui.Checkbox("Pause", ref paused))
        {
            renderer.Paused = paused;
        }

        ImGui.Separator();

        DLSSCapabilities capabilities = renderer.DLSSCapabilities;

        bool rayReconstruction = renderer.RayReconstruction;

        ImGui.BeginDisabled(!capabilities.RayReconstructionSupported);

        if (ImGui.Checkbox("Ray Reconstruction", ref rayReconstruction))
        {
            renderer.RayReconstruction = rayReconstruction;
        }

        ImGui.EndDisabled();

        ImGuiHelper.Tooltip(capabilities.RayReconstructionSupported ? null : "DLSS Ray Reconstruction is not available on this device.");

        bool frameGeneration = renderer.FrameGeneration;

        ImGui.BeginDisabled(!capabilities.FrameGenerationSupported);

        if (ImGui.Checkbox("Frame Generation", ref frameGeneration))
        {
            renderer.FrameGeneration = frameGeneration;
        }

        ImGui.EndDisabled();

        ImGuiHelper.Tooltip(capabilities.FrameGenerationSupported ? null : "DLSS Frame Generation is not available on this device.");
    }

    private static void CreateTargets()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            uiTextures[i] = Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, Width, Height, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.Sampled | TextureUsages.ColorAttachment
            });

            backBuffers[i] = Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, Width, Height, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.TransferSrc
            });

            generatedFrames[i] = Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, Width, Height, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.Storage | TextureUsages.TransferSrc | TextureUsages.TransferDst
            });
        }
    }

    private static void DestroyTargets()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            generatedFrames[i].Dispose();
            backBuffers[i].Dispose();
            uiTextures[i].Dispose();
        }
    }
}
