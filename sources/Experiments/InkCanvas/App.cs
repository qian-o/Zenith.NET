using System.Numerics;
using InkCanvas.Helpers;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Extensions.Skia;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace InkCanvas;

internal static class App
{
    private static readonly IWindow window;
    private static readonly IInputContext input;
    private static readonly SwapChain swapChain;
    private static readonly Board board;

    private static SKTexture texture;

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
            API = GraphicsAPI.None,
            Title = "Ink Canvas - Zenith.NET",
            Size = new(1280, 800)
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

        board = new();

        texture = Context.CreateSKTexture(new()
        {
            Format = PixelFormat.B8G8R8A8UNorm,
            Width = Width,
            Height = Height,
            SampleCount = SampleCount.Count1
        });
    }

    public static GraphicsContext Context { get; }

    public static uint Width => (uint)window.FramebufferSize.X;

    public static uint Height => (uint)window.FramebufferSize.Y;

    public static Vector2 DpiScale => (Vector2)window.FramebufferSize / (Vector2)window.Size;

    public static void Run()
    {
        window.Render += _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            uint width = (uint)(Width / DpiScale.X);
            uint height = (uint)(Height / DpiScale.Y);

            texture.Render((canvas) =>
            {
                canvas.Save();
                canvas.Scale(DpiScale.X, DpiScale.Y);

                board.Draw(canvas, Width / DpiScale.X, Height / DpiScale.Y);

                canvas.Restore();
            });

            CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

            commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.Undefined, TextureLayout.CopyDst);
            commandBuffer.Transition(texture, default, TextureLayout.ColorAttachment, TextureLayout.CopySrc);

            commandBuffer.CopyTexture(texture, default, default, swapChain.Drawable, default, default, new()
            {
                Width = width,
                Height = height,
                Depth = 1
            });

            commandBuffer.Transition(texture, default, TextureLayout.CopySrc, TextureLayout.ColorAttachment);
            commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.CopyDst, TextureLayout.Present);

            commandBuffer.Submit().Wait();

            swapChain.Present();
        };

        window.Resize += _ =>
        {
            if (Width is 0 || Height is 0)
            {
                return;
            }

            texture.Dispose();
            texture = Context.CreateSKTexture(new()
            {
                Format = PixelFormat.B8G8R8A8UNorm,
                Width = Width,
                Height = Height,
                SampleCount = SampleCount.Count1
            });

            swapChain.Resize(Width, Height);
        };

        IMouse mouse = input.Mice[0];
        mouse.MouseMove += (_, position) => board.PointerMove(new(position.X, position.Y));

        mouse.MouseDown += (_, button) =>
        {
            if (button is MouseButton.Left or MouseButton.Right)
            {
                board.PointerDown(new(mouse.Position.X, mouse.Position.Y), button is MouseButton.Right);
            }
        };

        mouse.MouseUp += (_, button) =>
        {
            if (button is MouseButton.Left or MouseButton.Right)
            {
                board.PointerUp(button is MouseButton.Right);
            }
        };

        window.Run();

        board.Dispose();
        texture.Dispose();
        swapChain.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();
    }
}