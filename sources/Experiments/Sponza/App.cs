using System.Numerics;
using Hexa.NET.ImGui;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Sponza.Handlers;
using Sponza.Helpers;
using Sponza.Models;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace Sponza;

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

        Context.ValidationMessage += static (sender, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");

        window = Window.Create(WindowOptions.Default with
        {
            Size = new(1280, 720),
            API = GraphicsAPI.None,
            Title = "Sponza - Zenith.NET"
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
            Format = PixelFormat.B8G8R8A8UNorm
        });

        imGui = new(input, new()
        {
            ColorFormats = [PixelFormat.B8G8R8A8UNorm],
            SampleCount = SampleCount.Count1
        });

        camera = new(input, Matrix4x4.CreateRotationY(float.DegreesToRadians(90.0f)) * Matrix4x4.CreateTranslation(new(0.0f, 1.2f, 0.0f)));

        renderer = new()
        {
            Settings = new()
            {
                RenderScale = 1.0f,
                UpscalingMode = UpscalingMode.None,
                TimeOfDay = 12.0f
            }
        };
    }

    public static GraphicsContext Context { get; }

    public static uint Width => (uint)window.FramebufferSize.X;

    public static uint Height => (uint)window.FramebufferSize.Y;

    public static Vector2 DpiScale => (Vector2)window.FramebufferSize / (Vector2)window.Size;

    public static void Run()
    {
        window.Update += delta =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            uint width = (uint)(Width / DpiScale.X);
            uint height = (uint)(Height / DpiScale.Y);

            imGui.Update(delta, width, height);
            camera.Update(delta, width, height);

            ImGui.GetBackgroundDrawList().AddImage(imGui.Binding(renderer.Color), new(0, 0), new(Width / DpiScale.X, Height / DpiScale.Y));

            ImGuiHelper.Overlay(() =>
            {
                ImGui.Text(Context.Capabilities.DeviceName);
                ImGui.Text($"GraphicsApi: {Context.GraphicsApi}");
                ImGui.Text($"FPS: {ImGui.GetIO().Framerate:F1}");
            });

            ImGuiHelper.Settings(() =>
            {
                ImGui.SliderFloat("Render scale", ref renderer.Settings.RenderScale, 0.5f, 1.0f, "%.2fx", ImGuiSliderFlags.AlwaysClamp);

                ImGui.Separator();

                if (ImGui.BeginCombo("Upscaling", renderer.Settings.UpscalingMode.ToString()))
                {
                    foreach (UpscalingMode upscalingMode in Enum.GetValues<UpscalingMode>())
                    {
                        bool selected = renderer.Settings.UpscalingMode == upscalingMode;

                        if (ImGui.Selectable(upscalingMode.ToString(), selected))
                        {
                            renderer.Settings.UpscalingMode = upscalingMode;
                        }

                        if (selected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }

                    ImGui.EndCombo();
                }

                ImGui.Separator();

                ImGui.SliderFloat("Time of day", ref renderer.Settings.TimeOfDay, 6.0f, 18.0f, "%.2f h", ImGuiSliderFlags.AlwaysClamp);
            });
        };

        window.Render += _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

            renderer.Update(camera);
            renderer.Render(commandBuffer);

            commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
            imGui.Render(commandBuffer, ColorAttachment.Clear(swapChain.Drawable, default));
            commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);

            commandBuffer.Submit().Wait();

            swapChain.Present();
        };

        window.Resize += _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            renderer.Resize(Width, Height);
            swapChain.Resize(Width, Height);
        };

        window.Run();

        renderer.Dispose();
        imGui.Dispose();
        swapChain.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();
    }
}
