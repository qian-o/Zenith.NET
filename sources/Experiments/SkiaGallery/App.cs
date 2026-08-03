using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Windowing;
using SkiaGallery.Helpers;
using SkiaSharp;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Extensions.Skia;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace SkiaGallery;

internal static class App
{
    private static readonly IWindow window;
    private static readonly IInputContext input;
    private static readonly SwapChain swapChain;
    private static readonly Gallery gallery;
    private static readonly Action<SKCanvas> drawGallery = DrawGallery;

    private static SKTexture texture;
    private static float logicalWidth;
    private static float logicalHeight;
    private static double totalSeconds;

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
            Title = "Skia Gallery - Zenith.NET",
            Size = new(1280, 800),
            Position = new(80, 60),
            IsVisible = true,
            FramesPerSecond = 60.0,
            UpdatesPerSecond = 60.0,
            VSync = true
        });
        window.Initialize();

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
        else if (window.Native?.X11 is { } x11)
        {
            surface = Surface.Xlib(x11.Display, (nint)x11.Window, Width, Height);
        }
        else
        {
            throw new PlatformNotSupportedException("SkiaGallery requires an X11 or XWayland window on Linux.");
        }

        swapChain = Context.CreateSwapChain(new()
        {
            Surface = surface,
            Format = PixelFormat.B8G8R8A8UNorm
        });

        texture = CreateTexture(Width, Height);
        gallery = new(Context.GraphicsApi, Context.Capabilities.DeviceName);
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

        IKeyboard keyboard = input.Keyboards[0];
        keyboard.KeyDown += KeyDown;

        window.Render += Render;

        try
        {
            window.Run();
        }
        finally
        {
            gallery.Dispose();
            texture.Dispose();
            swapChain.Dispose();
            input.Dispose();
            window.Dispose();
            Context.Dispose();
        }
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
        float nextLogicalWidth = width / dpiScale.X;
        float nextLogicalHeight = height / dpiScale.Y;
        bool viewportChanged = logicalWidth != nextLogicalWidth || logicalHeight != nextLogicalHeight;

        logicalWidth = nextLogicalWidth;
        logicalHeight = nextLogicalHeight;
        totalSeconds += Math.Min(delta, 0.1);

        bool resized = Resize(width, height);
        bool shouldRender = resized || viewportChanged || gallery.ShouldRender(totalSeconds);

        if (shouldRender)
        {
            texture.Render(drawGallery);
        }

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

    private static void DrawGallery(SKCanvas canvas)
    {
        Vector2 dpiScale = DpiScale;

        canvas.Save();
        canvas.Scale(dpiScale.X, dpiScale.Y);
        gallery.Draw(canvas, logicalWidth, logicalHeight, totalSeconds);
        canvas.Restore();
    }

    private static void MouseMove(IMouse _, Vector2 position)
    {
        gallery.PointerMove(position);
    }

    private static void MouseDown(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Left)
        {
            gallery.PointerDown(mouse.Position);
        }
    }

    private static void KeyDown(IKeyboard _, Key key, int code)
    {
        if (key is Key.Left)
        {
            gallery.Previous();
        }
        else if (key is Key.Right)
        {
            gallery.Next();
        }
        else if (key is Key.Home)
        {
            gallery.Select(0);
        }
        else if (key is Key.End)
        {
            gallery.Select(gallery.SceneCount - 1);
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

    private static bool Resize(uint width, uint height)
    {
        if (texture.Desc.Width == width && texture.Desc.Height == height)
        {
            return false;
        }

        swapChain.Resize(width, height);

        SKTexture oldTexture = texture;
        texture = CreateTexture(width, height);
        oldTexture.Dispose();

        return true;
    }
}