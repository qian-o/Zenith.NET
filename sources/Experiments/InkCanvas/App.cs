using System.Numerics;
using InkCanvas.Helpers;
using Silk.NET.Input;
using Silk.NET.Windowing;
using SkiaSharp;
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
    private static float logicalWidth;
    private static float logicalHeight;

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

        texture = CreateTexture(Width, Height);
        board = new();
    }

    public static GraphicsContext Context { get; }

    public static uint Width => (uint)window.FramebufferSize.X;

    public static uint Height => (uint)window.FramebufferSize.Y;

    public static Vector2 DpiScale => (Vector2)window.FramebufferSize / (Vector2)window.Size;

    public static void Run()
    {
        IMouse mouse = input.Mice[0];
        mouse.MouseMove += MouseMove;
        mouse.MouseDown += MouseDown;
        mouse.MouseUp += MouseUp;

        window.Render += Render;

        window.Run();

        board.Dispose();
        texture.Dispose();
        swapChain.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();
    }

    private static void Render(double delta)
    {
        uint width = Width;
        uint height = Height;

        if (width is 0 || height is 0)
        {
            return;
        }

        Vector2 dpiScale = DpiScale;
        logicalWidth = width / dpiScale.X;
        logicalHeight = height / dpiScale.Y;

        Resize(width, height);
        texture.Render(DrawBoard);

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
    }

    private static void DrawBoard(SKCanvas canvas)
    {
        Vector2 dpiScale = DpiScale;

        canvas.Save();
        canvas.Scale(dpiScale.X, dpiScale.Y);
        board.Draw(canvas, logicalWidth, logicalHeight);
        canvas.Restore();
    }

    private static void MouseMove(IMouse _, Vector2 position)
    {
        board.PointerMove(new(position.X, position.Y));
    }

    private static void MouseDown(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Left)
        {
            board.PointerDown(new(mouse.Position.X, mouse.Position.Y), erase: false);
        }
        else if (button is MouseButton.Right)
        {
            board.PointerDown(new(mouse.Position.X, mouse.Position.Y), erase: true);
        }
    }

    private static void MouseUp(IMouse _, MouseButton button)
    {
        if (button is MouseButton.Left or MouseButton.Right)
        {
            board.PointerUp();
        }
    }

    private static SKTexture CreateTexture(uint width, uint height)
    {
        return Context.CreateSKTexture(new()
        {
            Format = PixelFormat.B8G8R8A8UNorm,
            Width = width,
            Height = height,
            SampleCount = SampleCount.Count1
        });
    }

    private static void Resize(uint width, uint height)
    {
        if (texture.Desc.Width == width && texture.Desc.Height == height)
        {
            return;
        }

        swapChain.Resize(width, height);

        SKTexture oldTexture = texture;
        texture = CreateTexture(width, height);
        oldTexture.Dispose();
    }
}