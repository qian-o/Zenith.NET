using System.Numerics;
using Hexa.NET.ImGui;
using Silk.NET.Input;
using Zenith.NET;
using Zenith.NET.Extensions.ImGui;

namespace CornellBox.Handlers;

internal unsafe class ImGuiHandler : DisposableObject, IImGuiPlatform
{
    private readonly IMouse mouse;
    private readonly IKeyboard keyboard;
    private readonly Lock @lock = new();
    private readonly List<ImGuiInputArgs> inputs = [];

    private volatile StandardCursor pendingCursor;

    public ImGuiHandler(IInputContext input)
    {
        mouse = input.Mice[0];
        keyboard = input.Keyboards[0];

        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.MouseMove += OnMouseMove;
        mouse.Scroll += OnMouseScroll;

        keyboard.KeyDown += OnKeyDown;
        keyboard.KeyUp += OnKeyUp;
        keyboard.KeyChar += OnKeyChar;
    }

    public string ClipboardText
    {
        get => keyboard.ClipboardText;
        set => keyboard.ClipboardText = value;
    }

    public event EventHandler<ImGuiInputArgs>? Input;

    public void Initialize(ImGuiIOPtr io)
    {
        io.Fonts.Clear();
        io.Fonts.AddFontFromFileTTF(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "msyh.ttf"));
    }

    public void SetCursor(ImGuiMouseCursor cursor)
    {
        pendingCursor = cursor switch
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

    public void SetImeData(ImGuiViewportPtr viewport, ImGuiPlatformImeDataPtr data)
    {
    }

    public void Update()
    {
        using Lock.Scope _ = @lock.EnterScope();

        foreach (ImGuiInputArgs args in inputs)
        {
            Input?.Invoke(this, args);
        }
        inputs.Clear();
    }

    public void UpdateCursor(double deltaSeconds)
    {
        mouse.Cursor.StandardCursor = pendingCursor;
    }

    protected override void Destroy()
    {
        if (keyboard is not null)
        {
            keyboard.KeyChar -= OnKeyChar;
            keyboard.KeyUp -= OnKeyUp;
            keyboard.KeyDown -= OnKeyDown;
        }

        if (mouse is not null)
        {
            mouse.Scroll -= OnMouseScroll;
            mouse.MouseMove -= OnMouseMove;
            mouse.MouseUp -= OnMouseUp;
            mouse.MouseDown -= OnMouseDown;
        }
    }

    private void AddInput(ImGuiInputArgs args)
    {
        using Lock.Scope _ = @lock.EnterScope();

        inputs.Add(args);
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button is >= MouseButton.Left and <= MouseButton.Button5)
        {
            AddInput(new(ImGuiInput.MouseDown) { MouseButton = (ImGuiMouseButton)button });
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button is >= MouseButton.Left and <= MouseButton.Button5)
        {
            AddInput(new(ImGuiInput.MouseUp) { MouseButton = (ImGuiMouseButton)button });
        }
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        AddInput(new(ImGuiInput.MouseMove) { Position = position });
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel offset)
    {
        AddInput(new(ImGuiInput.MouseWheel) { Offset = new(offset.X, offset.Y) });
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        AddInput(new(ImGuiInput.KeyDown) { Key = TranslateInputKeyToImGuiKey(key) });
        AddInput(new(ImGuiInput.KeyDown) { Key = TranslateInputKeyToImGuiModifier(key) });
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        AddInput(new(ImGuiInput.KeyUp) { Key = TranslateInputKeyToImGuiKey(key) });
        AddInput(new(ImGuiInput.KeyUp) { Key = TranslateInputKeyToImGuiModifier(key) });
    }

    private void OnKeyChar(IKeyboard keyboard, char c)
    {
        AddInput(new(ImGuiInput.TextInput) { Character = c });
    }

    private static ImGuiKey TranslateInputKeyToImGuiKey(Key key)
    {
        if (key >= Key.Number0 && key <= Key.Number9)
        {
            return ImGuiKey.Key0 + (key - Key.Number0);
        }

        if (key >= Key.A && key <= Key.Z)
        {
            return ImGuiKey.A + (key - Key.A);
        }

        if (key >= Key.F1 && key <= Key.F24)
        {
            return ImGuiKey.F1 + (key - Key.F1);
        }

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
