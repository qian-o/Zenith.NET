using System.Numerics;
using Hexa.NET.ImGui;
using Silk.NET.Input;
using Zenith.NET;
using Zenith.NET.Extensions.ImGui;

namespace CornellBox.Handlers;

internal class ImGuiHandler : DisposableObject, IImGuiPlatform
{
    private readonly IMouse mouse;
    private readonly IKeyboard keyboard;

    public ImGuiHandler(IInputContext input)
    {
        mouse = input.Mice[0];
        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.MouseMove += OnMouseMove;
        mouse.Scroll += OnMouseScroll;

        keyboard = input.Keyboards[0];
        keyboard.KeyDown += OnKeyDown;
        keyboard.KeyUp += OnKeyUp;
        keyboard.KeyChar += OnKeyChar;
    }

    public event EventHandler<ImGuiInputArgs>? Input;

    public unsafe void Initialize(ImGuiIOPtr io)
    {
        io.Fonts.Clear();
        io.Fonts.AddFontFromFileTTF(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "msyh.ttf"));
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.DisplayFramebufferScale = App.DpiScale;
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

    protected override void Destroy()
    {
        mouse.MouseDown -= OnMouseDown;
        mouse.MouseUp -= OnMouseUp;
        mouse.MouseMove -= OnMouseMove;
        mouse.Scroll -= OnMouseScroll;

        keyboard.KeyDown -= OnKeyDown;
        keyboard.KeyUp -= OnKeyUp;
        keyboard.KeyChar -= OnKeyChar;
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        Input?.Invoke(this, new(ImGuiInput.MouseDown)
        {
            MouseButton = button switch
            {
                MouseButton.Left => ImGuiMouseButton.Left,
                MouseButton.Right => ImGuiMouseButton.Right,
                MouseButton.Middle => ImGuiMouseButton.Middle,
                _ => (int)ImGuiMouseButton.Count + (int)button - ImGuiMouseButton.Middle
            }
        });
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        Input?.Invoke(this, new(ImGuiInput.MouseUp)
        {
            MouseButton = button switch
            {
                MouseButton.Left => ImGuiMouseButton.Left,
                MouseButton.Right => ImGuiMouseButton.Right,
                MouseButton.Middle => ImGuiMouseButton.Middle,
                _ => (int)ImGuiMouseButton.Count + (int)button - ImGuiMouseButton.Middle
            }
        });
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        Input?.Invoke(this, new(ImGuiInput.MouseMove) { Position = position });
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel offset)
    {
        Input?.Invoke(this, new(ImGuiInput.MouseWheel) { Offset = new(offset.X, offset.Y) });
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        Input?.Invoke(this, new(ImGuiInput.KeyDown) { Key = TranslateInputKeyToImGuiKey(key) });
        Input?.Invoke(this, new(ImGuiInput.KeyDown) { Key = TranslateInputKeyToImGuiModifier(key) });
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        Input?.Invoke(this, new(ImGuiInput.KeyUp) { Key = TranslateInputKeyToImGuiKey(key) });
        Input?.Invoke(this, new(ImGuiInput.KeyUp) { Key = TranslateInputKeyToImGuiModifier(key) });
    }

    private void OnKeyChar(IKeyboard keyboard, char c)
    {
        Input?.Invoke(this, new(ImGuiInput.TextInput) { Character = c });
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
            >= Key.Number0 and <= Key.Number9 => ImGuiKey.Key0 + (key - Key.Number0),
            >= Key.A and <= Key.Z => ImGuiKey.A + (key - Key.A),
            >= Key.F1 and <= Key.F24 => ImGuiKey.F1 + (key - Key.F1),
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
}
