using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Windowing;
using SkiaBoard.Helpers;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Extensions.Skia;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace SkiaBoard;

internal static class App
{
    private static readonly IWindow window;
    private static readonly IInputContext input;
    private static readonly SwapChain swapChain;
    private static readonly Board board;

    private static SKTexture texture;
    private static bool controlDown;
    private static bool shiftDown;

    static App()
    {
        GraphicsApi graphicsApi = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault()?.ToLowerInvariant() switch
        {
            "dx12" => GraphicsApi.DirectX12,
            "vulkan" => GraphicsApi.Vulkan,
            "metal" => GraphicsApi.Metal,
            _ when OperatingSystem.IsMacOS() => GraphicsApi.Metal,
            _ when OperatingSystem.IsLinux() => GraphicsApi.Vulkan,
            _ => GraphicsApi.DirectX12
        };

        Context = graphicsApi switch
        {
            GraphicsApi.DirectX12 => GraphicsContext.CreateDirectX12(useValidationLayer: true),
            GraphicsApi.Metal => GraphicsContext.CreateMetal(useValidationLayer: true),
            GraphicsApi.Vulkan => GraphicsContext.CreateVulkan(useValidationLayer: true),
            _ => default!
        };

        Context.ValidationMessage += static (_, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");

        window = Window.Create(WindowOptions.Default with
        {
            API = GraphicsAPI.None,
            Title = $"Skia Board [{graphicsApi}]",
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
        mouse.MouseDown += MouseDown;
        mouse.MouseUp += MouseUp;
        mouse.MouseMove += MouseMove;
        mouse.Scroll += (_, wheel) => board.ResizeBrush(wheel.Y);

        IKeyboard keyboard = input.Keyboards[0];
        keyboard.KeyDown += KeyDown;
        keyboard.KeyUp += KeyUp;

        window.Render += Render;

        window.Run();

        texture.Dispose();
        swapChain.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();
    }

    private static void MouseDown(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Left && TryBoardSize(out float width, out float height))
        {
            board.PointerDown(new(mouse.Position.X, mouse.Position.Y), width, height);
        }
    }

    private static void MouseUp(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Left && TryBoardSize(out float width, out float height))
        {
            board.PointerUp(new(mouse.Position.X, mouse.Position.Y), width, height);
        }
    }

    private static void MouseMove(IMouse _, Vector2 position)
    {
        if (TryBoardSize(out float width, out float height))
        {
            board.PointerMove(new(position.X, position.Y), width, height);
        }
    }

    private static void KeyDown(IKeyboard _, Key key, int code)
    {
        if (key is Key.ControlLeft or Key.ControlRight)
        {
            controlDown = true;
        }
        else if (key is Key.ShiftLeft or Key.ShiftRight)
        {
            shiftDown = true;
        }
        else if (key is Key.Z && controlDown)
        {
            if (shiftDown)
            {
                board.Redo();
            }
            else
            {
                board.Undo();
            }
        }
        else if (key is Key.Y && controlDown)
        {
            board.Redo();
        }
        else if (key is Key.Delete)
        {
            board.Clear();
        }
        else if (key is Key.B)
        {
            board.UseBrush();
        }
        else if (key is Key.E)
        {
            board.UseEraser();
        }
    }

    private static void KeyUp(IKeyboard _, Key key, int code)
    {
        if (key is Key.ControlLeft or Key.ControlRight)
        {
            controlDown = false;
        }
        else if (key is Key.ShiftLeft or Key.ShiftRight)
        {
            shiftDown = false;
        }
    }

    private static void Render(double delta)
    {
        uint width = Width;
        uint height = Height;

        if (width is 0 || height is 0 || !TryBoardSize(out float boardWidth, out float boardHeight))
        {
            return;
        }

        Vector2 dpiScale = DpiScale;

        Resize(width, height);

        texture.Render(canvas =>
        {
            canvas.Save();
            canvas.Scale(dpiScale.X, dpiScale.Y);
            board.Draw(canvas, boardWidth, boardHeight);
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
        window.Title = $"Skia Board [{Context.GraphicsApi}] - {board.ToolName} {board.BrushWidth:0.#}px";
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

    private static bool TryBoardSize(out float width, out float height)
    {
        width = window.Size.X;
        height = window.Size.Y;

        return width > 0.0f && height > 0.0f;
    }
}
