using CornellBox.Models;
using Hexa.NET.ImGui;
using Silk.NET.Input;
using Zenith.NET.Extensions.ImGui;

namespace CornellBox.Handlers;

internal class ImGuiHandler(PlatformHandler platform) : IImGuiPlatform
{
    public event EventHandler<ImGuiInputArgs>? Input;

    public void Initialize(ImGuiIOPtr io)
    {
        io.DisplayFramebufferScale = platform.DpiScale;

        unsafe
        {
            io.Fonts.Clear();
            io.Fonts.AddFontFromFileTTF(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "msyh.ttf"));
        }
    }

    public void SetCursor(ImGuiMouseCursor cursor)
    {
        platform.Cursor = cursor switch
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
        return platform.ClipboardText;
    }

    public void SetClipboardText(string text)
    {
        platform.ClipboardText = text;
    }

    public void SetImeData(ImGuiViewportPtr viewport, ImGuiPlatformImeDataPtr data)
    {
    }

    public void Update(ReadOnlySpan<Input> inputs)
    {
        foreach (Input input in inputs)
        {
            switch (input.Type)
            {
                case InputType.MouseDown when input.Button <= MouseButton.Button5:
                    Input?.Invoke(this, new(ImGuiInput.MouseDown) { MouseButton = (ImGuiMouseButton)input.Button });
                    break;

                case InputType.MouseUp when input.Button <= MouseButton.Button5:
                    Input?.Invoke(this, new(ImGuiInput.MouseUp) { MouseButton = (ImGuiMouseButton)input.Button });
                    break;

                case InputType.MouseMove:
                    Input?.Invoke(this, new(ImGuiInput.MouseMove) { Position = input.Position });
                    break;

                case InputType.MouseWheel:
                    Input?.Invoke(this, new(ImGuiInput.MouseWheel) { Offset = input.Offset });
                    break;

                case InputType.KeyDown:
                case InputType.KeyUp:
                    ImGuiInput type = input.Type is InputType.KeyDown ? ImGuiInput.KeyDown : ImGuiInput.KeyUp;
                    ImGuiKey key = ToKey(input.Key);
                    if (key is not ImGuiKey.None)
                    {
                        Input?.Invoke(this, new(type) { Key = key });
                    }

                    ImGuiKey modifier = ToModifier(input.Key);
                    if (modifier is not ImGuiKey.None)
                    {
                        Input?.Invoke(this, new(type) { Key = modifier });
                    }
                    break;

                case InputType.TextInput:
                    Input?.Invoke(this, new(ImGuiInput.TextInput) { Character = input.Character });
                    break;
            }
        }
    }

    private static ImGuiKey ToKey(Key key)
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

        if (key >= Key.Keypad0 && key <= Key.Keypad9)
        {
            return ImGuiKey.Keypad0 + (key - Key.Keypad0);
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

    private static ImGuiKey ToModifier(Key key)
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
