using System.Numerics;
using Hexa.NET.ImGui;
using Silk.NET.Input;
using Silk.NET.Windowing;
using SponzaScene.Helpers;
using SponzaScene.Models;
using SponzaScene.Renderer;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Extensions.ImGui;
using Zenith.NET.Vulkan;

namespace SponzaScene;

internal static class App
{
    private static readonly IWindow window;
    private static readonly IInputContext inputContext;
    private static readonly SwapChain swapChain;
    private static readonly SilkImGuiController imGui;
    private static readonly CameraController camera;
    private static readonly DeferredRenderer renderer;

    static App()
    {
        Context = GraphicsContext.CreateVulkan(true);
        Context.ValidationMessage += static (sender, args) => Console.WriteLine($"[{args.Source} - {args.Severity}] {args.Message}");

        Sponza = new();

        FallbackTexture = Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8G8B8A8UNorm,
            Width = 1,
            Height = 1,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource
        });
        FallbackTexture.Upload([unchecked((int)0xFFFF00FF)], default, default, new() { Width = 1, Height = 1, Depth = 1 });

        PointSampler = Context.CreateSampler(new()
        {
            Filter = Filter.MinPointMagPointMipPoint,
            MaxLod = uint.MaxValue
        });

        LinearSampler = Context.CreateSampler(new()
        {
            Filter = Filter.MinLinearMagLinearMipLinear,
            MaxLod = uint.MaxValue
        });

        window = Window.Create(WindowOptions.Default with { API = GraphicsAPI.None });
        window.Initialize();

        inputContext = window.CreateInput();

        Surface surface;
        if (OperatingSystem.IsWindows())
        {
            surface = Surface.Win32(window.Native!.Win32!.Value.Hwnd, (uint)window.Size.X, (uint)window.Size.Y);
        }
        else if (OperatingSystem.IsLinux())
        {
            surface = Surface.Xlib(window.Native!.X11!.Value.Display, (nint)window.Native.X11.Value.Window, (uint)window.Size.X, (uint)window.Size.Y);
        }
        else
        {
            throw new PlatformNotSupportedException();
        }

        swapChain = Context.CreateSwapChain(new() { Surface = surface, ColorTargetFormat = PixelFormat.R8G8B8A8UNorm, DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt });
        imGui = new(inputContext, swapChain.FrameBuffer.Output, ImGuiColorSpace.Legacy);
        camera = new(inputContext, Matrix4x4.CreateRotationY(float.DegreesToRadians(90.0f)) * Matrix4x4.CreateTranslation(new Vector3(0.0f, 1.2f, 0.0f)));
        renderer = new();
    }

    public static GraphicsContext Context { get; }

    public static Sponza Sponza { get; }

    public static Texture FallbackTexture { get; }

    public static Sampler PointSampler { get; }

    public static Sampler LinearSampler { get; }

    public static void Run()
    {
        window.Update += delta =>
        {
            uint width = (uint)window.Size.X;
            uint height = (uint)window.Size.Y;

            imGui.Update(delta, width, height);
            camera.Update(delta, width, height);
            renderer.Update(width, height, camera);
        };

        window.Render += _ =>
        {
            if (window.Size.X is 0 || window.Size.Y is 0)
            {
                return;
            }

            renderer.Render();

            // ImGui Rendering
            {
                renderer.UI();

                ImGuiHelpers.Overlay("Info", () =>
                {
                    ImGui.Text($"Backend: {Context.Backend}");

                    ImGui.Separator();

                    ImGui.Text(Context.Capabilities.DeviceName);

                    ImGui.Separator();

                    ImGui.Text($"Ray Tracing Supported: {Context.Capabilities.RayTracingSupported}");

                    ImGui.Separator();

                    ImGui.Text($"Mesh Shader Supported: {Context.Capabilities.MeshShaderSupported}");

                    ImGui.Separator();

                    ImGui.Text($"Current FPS: {ImGui.GetIO().Framerate:F1}");
                });

                CommandBuffer commandBuffer = Context.Graphics.CommandBuffer();

                imGui.Render(commandBuffer, swapChain.FrameBuffer, ClearValues.Default);

                commandBuffer.Submit(true);
            }

            swapChain.Present();
        };

        window.Resize += size =>
        {
            if (size.X is 0 || size.Y is 0)
            {
                return;
            }

            swapChain.Resize((uint)size.X, (uint)size.Y);
        };

        window.Run();

        renderer.Dispose();
        imGui.Dispose();
        swapChain.Dispose();
        inputContext.Dispose();
        window.Dispose();

        PointSampler.Dispose();
        LinearSampler.Dispose();
        FallbackTexture.Dispose();
        Sponza.Dispose();
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
