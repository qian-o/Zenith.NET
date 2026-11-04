using System.Numerics;
using InkCanvas.Drawing;
using InkCanvas.Helpers;
using Silk.NET.Input;
using Silk.NET.Maths;
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
    private static readonly Canvas canvas;
    private static readonly CanvasPresenter presenter;

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

        Context.ValidationMessage += static (_, args) =>
        {
            // These validation messages are caused by known issues in Skia's D3D12 backend.
            if (args.Message.StartsWith("ID3D12DescriptorHeap::GetGPUDescriptorHandleForHeapStart:", StringComparison.Ordinal)
                || args.Message.StartsWith("ID3D12Device::CreateSampler2:", StringComparison.Ordinal)
                || args.Message.StartsWith("ID3D12CommandList::ClearRenderTargetView:", StringComparison.Ordinal))
            {
                return;
            }

            Console.WriteLine($"[{args.Severity}] {args.Message}");
        };

        Context.InitializeSkia();

        window = Window.Create(WindowOptions.Default with
        {
            Size = new(1280, 800),
            API = GraphicsAPI.None,
            Title = "Ink Canvas - Zenith.NET"
        });
        window.Initialize();
        window.Center();

        input = window.CreateInput();

        Surface surface;
        if (OperatingSystem.IsWindows())
        {
            surface = Surface.Win32(window.Native!.Win32!.Value.Hwnd, PhysicalWidth, PhysicalHeight);
        }
        else if (OperatingSystem.IsMacOS())
        {
            surface = Surface.Apple(CocoaHelper.CreateLayer(window.Native!.Cocoa!.Value), PhysicalWidth, PhysicalHeight);
        }
        else
        {
            surface = Surface.Xlib(window.Native!.X11!.Value.Display, (nint)window.Native.X11.Value.Window, PhysicalWidth, PhysicalHeight);
        }

        canvas = new();
        presenter = new(surface);
    }

    public static GraphicsContext Context { get; }

    public static uint LogicalWidth => (uint)window.Size.X;

    public static uint LogicalHeight => (uint)window.Size.Y;

    public static uint PhysicalWidth => (uint)window.FramebufferSize.X;

    public static uint PhysicalHeight => (uint)window.FramebufferSize.Y;

    public static Vector2 DpiScale => (Vector2)window.FramebufferSize / (Vector2)window.Size;

    public static void Run()
    {
        window.Render += OnRender;
        window.Resize += OnResize;
        window.FramebufferResize += OnFramebufferResize;

        input.Mice[0].MouseDown += OnMouseDown;
        input.Mice[0].MouseUp += OnMouseUp;
        input.Mice[0].MouseMove += OnMouseMove;

        window.Run();

        input.Mice[0].MouseMove -= OnMouseMove;
        input.Mice[0].MouseUp -= OnMouseUp;
        input.Mice[0].MouseDown -= OnMouseDown;

        window.Resize -= OnResize;
        window.FramebufferResize -= OnFramebufferResize;
        window.Render -= OnRender;

        presenter.Dispose();
        canvas.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();
    }

    private static void OnRender(double deltaSeconds)
    {
        if (PhysicalWidth is 0 || PhysicalHeight is 0)
        {
            return;
        }

        presenter.Present(canvas);
    }

    private static void OnResize(Vector2D<int> size)
    {
        if (LogicalWidth is 0 || LogicalHeight is 0)
        {
            return;
        }

        canvas.Resize();
    }

    private static void OnFramebufferResize(Vector2D<int> size)
    {
        if (PhysicalWidth is 0 || PhysicalHeight is 0)
        {
            return;
        }

        presenter.Resize();
    }

    private static void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Left or MouseButton.Right)
        {
            canvas.PointerDown(new(mouse.Position.X, mouse.Position.Y), button is MouseButton.Right);
        }
    }

    private static void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Left or MouseButton.Right)
        {
            canvas.PointerUp(button is MouseButton.Right);
        }
    }

    private static void OnMouseMove(IMouse mouse, Vector2 position)
    {
        canvas.PointerMove(new(position.X, position.Y));
    }
}
