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
    private static readonly IMouse mouse;
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
        mouse = input.Mice[0];
        canvas = new();
        canvas.Resize(window.Size.X, window.Size.Y);

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

        presenter = new(Context, surface, canvas);
    }

    public static GraphicsContext Context { get; }

    public static uint Width => (uint)window.FramebufferSize.X;

    public static uint Height => (uint)window.FramebufferSize.Y;

    public static Vector2 DpiScale => (Vector2)window.FramebufferSize / (Vector2)window.Size;

    public static void Run()
    {
        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.MouseMove += OnMouseMove;
        window.Render += OnRender;
        window.Resize += OnResize;
        window.FramebufferResize += OnResize;
        window.FocusChanged += OnFocusChanged;

        window.Run();

        window.FocusChanged -= OnFocusChanged;
        window.FramebufferResize -= OnResize;
        window.Resize -= OnResize;
        window.Render -= OnRender;
        mouse.MouseMove -= OnMouseMove;
        mouse.MouseUp -= OnMouseUp;
        mouse.MouseDown -= OnMouseDown;

        presenter.Dispose();
        canvas.Dispose();
        input.Dispose();
        window.Dispose();

        Context.Dispose();
    }

    private static void OnRender(double deltaSeconds)
    {
        if (Width is 0 || Height is 0 || window.Size.X is 0 || window.Size.Y is 0)
        {
            return;
        }

        presenter.Present(DpiScale);
    }

    private static void OnResize(Vector2D<int> size)
    {
        if (Width is 0 || Height is 0 || window.Size.X is 0 || window.Size.Y is 0)
        {
            return;
        }

        canvas.Resize(window.Size.X, window.Size.Y);
        presenter.Resize(Width, Height);
    }

    private static void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Left or MouseButton.Right)
        {
            bool isMsaaEnabled = canvas.IsMsaaEnabled;

            canvas.PointerDown(new(mouse.Position.X, mouse.Position.Y), button is MouseButton.Right);

            if (canvas.IsMsaaEnabled != isMsaaEnabled)
            {
                presenter.SetMultisamplingEnabled(canvas.IsMsaaEnabled);
            }
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

    private static void OnFocusChanged(bool isFocused)
    {
        if (!isFocused)
        {
            canvas.PointerUp(erase: false);
            canvas.PointerUp(erase: true);
        }
    }
}
