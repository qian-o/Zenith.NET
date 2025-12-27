using Hexa.NET.ImGui;
using Silk.NET.Input;
using Silk.NET.Windowing;
using SponzaScene.Helpers;
using SponzaScene.Models;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Extensions.ImGui;
using Zenith.NET.Vulkan;

namespace SponzaScene;

internal static class App
{
    static App()
    {
        MainWindow = Window.Create(WindowOptions.Default with { API = GraphicsAPI.None });
        MainWindow.Initialize();

        Context = GraphicsContext.CreateDirectX12(true);
        Context.ValidationMessage += static (sender, args) => Console.WriteLine($"[{args.Source} - {args.Severity}] {args.Message}");

        Surface surface;
        if (OperatingSystem.IsWindows())
        {
            surface = Surface.Win32(MainWindow.Native!.Win32!.Value.Hwnd, (uint)MainWindow.Size.X, (uint)MainWindow.Size.Y);
        }
        else if (OperatingSystem.IsLinux())
        {
            surface = Surface.Xlib(MainWindow.Native!.X11!.Value.Display, (nint)MainWindow.Native.X11.Value.Window, (uint)MainWindow.Size.X, (uint)MainWindow.Size.Y);
        }
        else
        {
            throw new PlatformNotSupportedException();
        }

        SwapChain = Context.CreateSwapChain(new() { Surface = surface, ColorTargetFormat = PixelFormat.R8G8B8A8UNorm, DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt });

        ImGuiController = new SilkImGuiController(MainWindow.CreateInput(), SwapChain.FrameBuffer.Output, ImGuiColorSpace.Legacy);

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

        LinearSampler = Context.CreateSampler(new()
        {
            Filter = Filter.MinLinearMagLinearMipLinear,
            MaxLod = uint.MaxValue
        });

        MainView = new();
    }

    public static IWindow MainWindow { get; }

    public static GraphicsContext Context { get; }

    public static SwapChain SwapChain { get; }

    public static ImGuiController ImGuiController { get; }

    public static Sponza Sponza { get; }

    public static Texture FallbackTexture { get; }

    public static Sampler LinearSampler { get; }

    public static MainView MainView { get; }

    public static void Run()
    {
        MainWindow.Update += delta =>
        {
            ImGuiController.Update(delta, (uint)MainWindow.Size.X, (uint)MainWindow.Size.Y);

            MainView.Update((uint)MainWindow.Size.X, (uint)MainWindow.Size.Y);
        };

        MainWindow.Render += delta =>
        {
            if (MainWindow.Size.X is 0 || MainWindow.Size.Y is 0)
            {
                return;
            }

            MainView.Render();

            ImGuiHelpers.Overlay("Info", () =>
            {
                ImGui.Text($"Backend: {Context.Backend}");

                ImGui.Separator();

                ImGui.Text(Context.Capabilities.DeviceName);

                ImGui.Separator();

                ImGui.Text($"Ray Tracing Supported: {Context.Capabilities.RayTracingSupported}");

                ImGui.Separator();

                ImGui.Text($"Mesh Shader Supported: {Context.Capabilities.MeshShaderSupported}");
            });

            CommandBuffer commandBuffer = Context.Graphics.CommandBuffer();

            commandBuffer.BindFrameBuffer(SwapChain.FrameBuffer, ClearValues.None);

            ImGuiController.Render(commandBuffer);

            commandBuffer.Submit();

            Context.Copy.WaitIdle();
            Context.Compute.WaitIdle();
            Context.Graphics.WaitIdle();

            SwapChain.Present();
        };

        MainWindow.Resize += size =>
        {
            if (size.X is 0 || size.Y is 0)
            {
                return;
            }

            SwapChain.Resize((uint)size.X, (uint)size.Y);
        };

        MainWindow.Run();

        MainView.Dispose();
        LinearSampler.Dispose();
        FallbackTexture.Dispose();
        Sponza.Dispose();
        ImGuiController.Dispose();
        SwapChain.Dispose();
        Context.Dispose();

        Console.WriteLine("Exited cleanly.");
    }
}
