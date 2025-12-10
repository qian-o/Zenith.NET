using System.Numerics;
using Hexa.NET.ImGui;
using Silk.NET.Input;
using Silk.NET.Windowing;
using SponzaScene.Helpers;
using Zenith.NET;
using Zenith.NET.Extensions.ImGui;
using Zenith.NET.Vulkan;

namespace SponzaScene;

internal class SilkImGuiController : ImGuiController, IImGuiPlatformBindings
{
    private readonly IMouse mouse;
    private readonly IKeyboard keyboard;

    public SilkImGuiController(IInputContext inputContext, Output output, ImGuiColorSpace colorSpace) : base(App.Context, output, colorSpace, Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "msyh.ttf"), OtherSetup)
    {
        mouse = inputContext.Mice[0];
        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.MouseMove += OnMouseMove;
        mouse.Scroll += OnMouseScroll;

        keyboard = inputContext.Keyboards[0];
        keyboard.KeyDown += OnKeyDown;
        keyboard.KeyUp += OnKeyUp;
        keyboard.KeyChar += OnKeyChar;

        PlatformBindings = this;
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        MouseDown(button switch
        {
            MouseButton.Left => ImGuiMouseButton.Left,
            MouseButton.Right => ImGuiMouseButton.Right,
            MouseButton.Middle => ImGuiMouseButton.Middle,
            _ => (int)ImGuiMouseButton.Count + (int)button - ImGuiMouseButton.Middle
        });
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        MouseUp(button switch
        {
            MouseButton.Left => ImGuiMouseButton.Left,
            MouseButton.Right => ImGuiMouseButton.Right,
            MouseButton.Middle => ImGuiMouseButton.Middle,
            _ => (int)ImGuiMouseButton.Count + (int)button - ImGuiMouseButton.Middle
        });
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        MouseMove(position);
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel offset)
    {
        MouseWheel(new(offset.X, offset.Y));
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        KeyDown(TranslateInputKeyToImGuiKey(key));
        KeyDown(TranslateInputKeyToImGuiModifier(key));
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int arg3)
    {
        KeyUp(TranslateInputKeyToImGuiKey(key));
        KeyUp(TranslateInputKeyToImGuiModifier(key));
    }

    private void OnKeyChar(IKeyboard keyboard, char c)
    {
        KeyChar(c);
    }

    private static ImGuiKey TranslateInputKeyToImGuiKey(Key key)
    {
        return key switch
        {
            Key.Tab => ImGuiKey.Tab,
            Key.Left => ImGuiKey.LeftArrow,
            Key.Right => ImGuiKey.RightArrow,
            Key.Up => ImGuiKey.UpArrow,
            Key.Down => ImGuiKey.DownArrow,
            Key.PageUp => ImGuiKey.PageUp,
            Key.PageDown => ImGuiKey.PageDown,
            Key.Home => ImGuiKey.Home,
            Key.End => ImGuiKey.End,
            Key.Insert => ImGuiKey.Insert,
            Key.Delete => ImGuiKey.Delete,
            Key.Backspace => ImGuiKey.Backspace,
            Key.Space => ImGuiKey.Space,
            Key.Enter => ImGuiKey.Enter,
            Key.Escape => ImGuiKey.Escape,
            Key.Apostrophe => ImGuiKey.Apostrophe,
            Key.Comma => ImGuiKey.Comma,
            Key.Minus => ImGuiKey.Minus,
            Key.Period => ImGuiKey.Period,
            Key.Slash => ImGuiKey.Slash,
            Key.Semicolon => ImGuiKey.Semicolon,
            Key.Equal => ImGuiKey.Equal,
            Key.LeftBracket => ImGuiKey.LeftBracket,
            Key.BackSlash => ImGuiKey.Backslash,
            Key.RightBracket => ImGuiKey.RightBracket,
            Key.GraveAccent => ImGuiKey.GraveAccent,
            Key.CapsLock => ImGuiKey.CapsLock,
            Key.ScrollLock => ImGuiKey.ScrollLock,
            Key.NumLock => ImGuiKey.NumLock,
            Key.PrintScreen => ImGuiKey.PrintScreen,
            Key.Pause => ImGuiKey.Pause,
            Key.Keypad0 => ImGuiKey.Keypad0,
            Key.Keypad1 => ImGuiKey.Keypad1,
            Key.Keypad2 => ImGuiKey.Keypad2,
            Key.Keypad3 => ImGuiKey.Keypad3,
            Key.Keypad4 => ImGuiKey.Keypad4,
            Key.Keypad5 => ImGuiKey.Keypad5,
            Key.Keypad6 => ImGuiKey.Keypad6,
            Key.Keypad7 => ImGuiKey.Keypad7,
            Key.Keypad8 => ImGuiKey.Keypad8,
            Key.Keypad9 => ImGuiKey.Keypad9,
            Key.KeypadDecimal => ImGuiKey.KeypadDecimal,
            Key.KeypadDivide => ImGuiKey.KeypadDivide,
            Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
            Key.KeypadSubtract => ImGuiKey.KeypadSubtract,
            Key.KeypadAdd => ImGuiKey.KeypadAdd,
            Key.KeypadEnter => ImGuiKey.KeypadEnter,
            Key.KeypadEqual => ImGuiKey.KeypadEqual,
            Key.ShiftLeft => ImGuiKey.LeftShift,
            Key.ControlLeft => ImGuiKey.LeftCtrl,
            Key.AltLeft => ImGuiKey.LeftAlt,
            Key.SuperLeft => ImGuiKey.LeftSuper,
            Key.ShiftRight => ImGuiKey.RightShift,
            Key.ControlRight => ImGuiKey.RightCtrl,
            Key.AltRight => ImGuiKey.RightAlt,
            Key.SuperRight => ImGuiKey.RightSuper,
            Key.Menu => ImGuiKey.Menu,
            Key.Number0 => ImGuiKey.Key0,
            Key.Number1 => ImGuiKey.Key1,
            Key.Number2 => ImGuiKey.Key2,
            Key.Number3 => ImGuiKey.Key3,
            Key.Number4 => ImGuiKey.Key4,
            Key.Number5 => ImGuiKey.Key5,
            Key.Number6 => ImGuiKey.Key6,
            Key.Number7 => ImGuiKey.Key7,
            Key.Number8 => ImGuiKey.Key8,
            Key.Number9 => ImGuiKey.Key9,
            Key.A => ImGuiKey.A,
            Key.B => ImGuiKey.B,
            Key.C => ImGuiKey.C,
            Key.D => ImGuiKey.D,
            Key.E => ImGuiKey.E,
            Key.F => ImGuiKey.F,
            Key.G => ImGuiKey.G,
            Key.H => ImGuiKey.H,
            Key.I => ImGuiKey.I,
            Key.J => ImGuiKey.J,
            Key.K => ImGuiKey.K,
            Key.L => ImGuiKey.L,
            Key.M => ImGuiKey.M,
            Key.N => ImGuiKey.N,
            Key.O => ImGuiKey.O,
            Key.P => ImGuiKey.P,
            Key.Q => ImGuiKey.Q,
            Key.R => ImGuiKey.R,
            Key.S => ImGuiKey.S,
            Key.T => ImGuiKey.T,
            Key.U => ImGuiKey.U,
            Key.V => ImGuiKey.V,
            Key.W => ImGuiKey.W,
            Key.X => ImGuiKey.X,
            Key.Y => ImGuiKey.Y,
            Key.Z => ImGuiKey.Z,
            Key.F1 => ImGuiKey.F1,
            Key.F2 => ImGuiKey.F2,
            Key.F3 => ImGuiKey.F3,
            Key.F4 => ImGuiKey.F4,
            Key.F5 => ImGuiKey.F5,
            Key.F6 => ImGuiKey.F6,
            Key.F7 => ImGuiKey.F7,
            Key.F8 => ImGuiKey.F8,
            Key.F9 => ImGuiKey.F9,
            Key.F10 => ImGuiKey.F10,
            Key.F11 => ImGuiKey.F11,
            Key.F12 => ImGuiKey.F12,
            Key.F13 => ImGuiKey.F13,
            Key.F14 => ImGuiKey.F14,
            Key.F15 => ImGuiKey.F15,
            Key.F16 => ImGuiKey.F16,
            Key.F17 => ImGuiKey.F17,
            Key.F18 => ImGuiKey.F18,
            Key.F19 => ImGuiKey.F19,
            Key.F20 => ImGuiKey.F20,
            Key.F21 => ImGuiKey.F21,
            Key.F22 => ImGuiKey.F22,
            Key.F23 => ImGuiKey.F23,
            Key.F24 => ImGuiKey.F24,
            _ => ImGuiKey.None
        };
    }

    private static ImGuiKey TranslateInputKeyToImGuiModifier(Key key)
    {
        return key switch
        {
            Key.ShiftLeft or Key.ShiftRight => ImGuiKey.ModShift,
            Key.ControlLeft or Key.ControlRight => ImGuiKey.ModCtrl,
            Key.AltLeft or Key.AltRight => ImGuiKey.ModAlt,
            Key.SuperLeft or Key.SuperRight => ImGuiKey.ModSuper,
            _ => ImGuiKey.None
        };
    }

    private static void OtherSetup(ImGuiIOPtr io)
    {
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
    }

    public void SetCursor(ImGuiMouseCursor cursor)
    {
        mouse.Cursor.StandardCursor = cursor switch
        {
            ImGuiMouseCursor.Arrow => StandardCursor.Arrow,
            ImGuiMouseCursor.TextInput => StandardCursor.IBeam,
            ImGuiMouseCursor.ResizeAll => StandardCursor.ResizeAll,
            ImGuiMouseCursor.ResizeNs => StandardCursor.VResize,
            ImGuiMouseCursor.ResizeEw => StandardCursor.HResize,
            ImGuiMouseCursor.ResizeNesw => StandardCursor.NeswResize,
            ImGuiMouseCursor.ResizeNwse => StandardCursor.NwseResize,
            ImGuiMouseCursor.Hand => StandardCursor.Hand,
            ImGuiMouseCursor.NotAllowed => StandardCursor.NotAllowed,
            _ => StandardCursor.Default
        };
    }

    public string GetClipboardText()
    {
        return keyboard.ClipboardText;
    }

    public void SetClipboardText(string text)
    {
        keyboard.ClipboardText = text;
    }

    public void SetImeData(ImGuiViewportPtr viewport, ImGuiPlatformImeDataPtr data)
    {
        // IME not supported.
    }
}

internal static class Dispatcher
{
    private static readonly Lock @lock = new();
    private static readonly Queue<Action> actions = new();

    public static void Invoke(Action action)
    {
        using Lock.Scope _ = @lock.EnterScope();

        actions.Enqueue(action);
    }

    public static void Process()
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (actions.Count > 0)
        {
            Action?[] actions = new Action?[Environment.ProcessorCount];

            for (int i = 0; i < actions.Length; i++)
            {
                if (Dispatcher.actions.Count > 0)
                {
                    actions[i] = Dispatcher.actions.Dequeue();
                }
            }

            Parallel.ForEach(actions, action => action?.Invoke());
        }
    }
}

internal interface IView : IDisposable
{
    void Update(double delta);

    void Render(double delta);
}

internal static class App
{
    static App()
    {
        MainWindow = Window.Create(WindowOptions.Default with { API = GraphicsAPI.None });
        MainWindow.Initialize();

        Context = GraphicsContext.CreateVulkan(true);
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
    }

    public static IWindow MainWindow { get; }

    public static GraphicsContext Context { get; }

    public static SwapChain SwapChain { get; }

    public static ImGuiController ImGuiController { get; }

    public static NewSponza Sponza { get; }

    public static List<IView> Views { get; } = [];

    public static void Run()
    {
        MainWindow.Update += delta =>
        {
            ImGuiController.Update(delta, (uint)MainWindow.Size.X, (uint)MainWindow.Size.Y);

            ImGui.DockSpaceOverViewport(null, ImGuiDockNodeFlags.PassthruCentralNode, null);

            foreach (IView view in Views)
            {
                view.Update(delta);
            }

            Dispatcher.Process();
        };

        MainWindow.Render += delta =>
        {
            if (MainWindow.Size.X is 0 || MainWindow.Size.Y is 0)
            {
                return;
            }

            foreach (IView view in Views)
            {
                view.Render(delta);
            }

            CommandBuffer commandBuffer = Context.Graphics.CommandBuffer();

            commandBuffer.BindFrameBuffer(SwapChain.FrameBuffer, ClearValues.Default);

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

        foreach (IView view in Views)
        {
            view.Dispose();
        }

        Sponza.Dispose();
        ImGuiController.Dispose();
        SwapChain.Dispose();
        Context.Dispose();

        Console.WriteLine("Exited cleanly.");
    }
}
