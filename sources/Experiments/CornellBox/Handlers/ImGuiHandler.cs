using Hexa.NET.ImGui;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Zenith.NET.Extensions.ImGui;

namespace CornellBox.Handlers;

internal unsafe class ImGuiHandler(IWindow window, IInputContext input) : IImGuiPlatform
{
    private readonly IMouse mouse = input.Mice[0];
    private readonly IKeyboard keyboard = input.Keyboards[0];
    private readonly HashSet<Key> keyDowns = [];
    private readonly HashSet<MouseButton> mouseDowns = [];

    private int requestedCursor = (int)StandardCursor.Default;
    private StandardCursor appliedCursor = StandardCursor.Default;
    private string clipboardText = input.Keyboards[0].ClipboardText;
    private string? requestedClipboardText;

    public event EventHandler<ImGuiInputArgs>? Input;

    public void Initialize(ImGuiIOPtr io)
    {
        io.DisplayFramebufferScale = App.DpiScale;

        io.Fonts.Clear();
        io.Fonts.AddFontFromFileTTF(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "msyh.ttf"));
    }

    public void SetCursor(ImGuiMouseCursor cursor)
    {
        StandardCursor value = cursor switch
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

        if (Interlocked.Exchange(ref requestedCursor, (int)value) != (int)value)
        {
            window.ContinueEvents();
        }
    }

    public string GetClipboardText()
    {
        return Volatile.Read(ref requestedClipboardText) ?? Volatile.Read(ref clipboardText);
    }

    public void SetClipboardText(string text)
    {
        Volatile.Write(ref requestedClipboardText, text);
        window.ContinueEvents();
    }

    public void SetImeData(ImGuiViewportPtr viewport, ImGuiPlatformImeDataPtr data)
    {
    }

    public void UpdatePlatform()
    {
        StandardCursor cursor = (StandardCursor)Volatile.Read(ref requestedCursor);
        if (appliedCursor != cursor)
        {
            mouse.Cursor.StandardCursor = cursor;
            appliedCursor = cursor;
        }

        string? text = Volatile.Read(ref requestedClipboardText);
        if (text is not null)
        {
            keyboard.ClipboardText = text;
        }

        Volatile.Write(ref clipboardText, keyboard.ClipboardText);

        if (text is not null)
        {
            Interlocked.CompareExchange(ref requestedClipboardText, null, text);
        }
    }

    public void ProcessInput(ReadOnlySpan<InputEvent> inputs)
    {
        foreach (InputEvent input in inputs)
        {
            switch (input.Type)
            {
                case InputEventType.MouseDown when input.MouseButton >= MouseButton.Left && input.MouseButton <= MouseButton.Button5:
                    mouseDowns.Add(input.MouseButton);
                    SendMouseButton(input.MouseButton, isDown: true);
                    break;

                case InputEventType.MouseUp when input.MouseButton >= MouseButton.Left && input.MouseButton <= MouseButton.Button5:
                    mouseDowns.Remove(input.MouseButton);
                    SendMouseButton(input.MouseButton, isDown: false);
                    break;

                case InputEventType.MouseMove:
                    Input?.Invoke(this, new(ImGuiInput.MouseMove) { Position = input.Position });
                    break;

                case InputEventType.MouseWheel:
                    Input?.Invoke(this, new(ImGuiInput.MouseWheel) { Offset = input.Offset });
                    break;

                case InputEventType.KeyDown:
                    keyDowns.Add(input.Key);
                    SendKey(input.Key, isDown: true);
                    break;

                case InputEventType.KeyUp:
                    keyDowns.Remove(input.Key);
                    SendKey(input.Key, isDown: false);
                    break;

                case InputEventType.TextInput:
                    Input?.Invoke(this, new(ImGuiInput.TextInput) { Character = input.Character });
                    break;

                case InputEventType.Reset:
                    ResetInput();
                    break;
            }
        }
    }

    private void SendMouseButton(MouseButton button, bool isDown)
    {
        // Both APIs assign the same indices to the five supported mouse buttons.
        Input?.Invoke(this, new(isDown ? ImGuiInput.MouseDown : ImGuiInput.MouseUp) { MouseButton = (ImGuiMouseButton)button });
    }

    private void SendKey(Key key, bool isDown)
    {
        ImGuiKey translated = TranslateInputKeyToImGuiKey(key);
        if (translated is not ImGuiKey.None)
        {
            Input?.Invoke(this, new(isDown ? ImGuiInput.KeyDown : ImGuiInput.KeyUp) { Key = translated });
        }

        (ImGuiKey modifier, bool isModifierDown) = key switch
        {
            Key.ShiftLeft or Key.ShiftRight => (ImGuiKey.ModShift, keyDowns.Contains(Key.ShiftLeft) || keyDowns.Contains(Key.ShiftRight)),
            Key.ControlLeft or Key.ControlRight => (ImGuiKey.ModCtrl, keyDowns.Contains(Key.ControlLeft) || keyDowns.Contains(Key.ControlRight)),
            Key.AltLeft or Key.AltRight => (ImGuiKey.ModAlt, keyDowns.Contains(Key.AltLeft) || keyDowns.Contains(Key.AltRight)),
            Key.SuperLeft or Key.SuperRight => (ImGuiKey.ModSuper, keyDowns.Contains(Key.SuperLeft) || keyDowns.Contains(Key.SuperRight)),
            _ => default
        };

        if (modifier is not ImGuiKey.None)
        {
            Input?.Invoke(this, new(isModifierDown ? ImGuiInput.KeyDown : ImGuiInput.KeyUp) { Key = modifier });
        }
    }

    private void ResetInput()
    {
        foreach (MouseButton button in mouseDowns)
        {
            SendMouseButton(button, isDown: false);
        }
        mouseDowns.Clear();

        foreach (Key key in keyDowns)
        {
            ImGuiKey translated = TranslateInputKeyToImGuiKey(key);
            if (translated is not ImGuiKey.None)
            {
                Input?.Invoke(this, new(ImGuiInput.KeyUp) { Key = translated });
            }
        }
        keyDowns.Clear();

        Input?.Invoke(this, new(ImGuiInput.KeyUp) { Key = ImGuiKey.ModShift });
        Input?.Invoke(this, new(ImGuiInput.KeyUp) { Key = ImGuiKey.ModCtrl });
        Input?.Invoke(this, new(ImGuiInput.KeyUp) { Key = ImGuiKey.ModAlt });
        Input?.Invoke(this, new(ImGuiInput.KeyUp) { Key = ImGuiKey.ModSuper });
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
}
