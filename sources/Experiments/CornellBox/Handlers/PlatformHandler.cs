using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Zenith.NET;

namespace CornellBox.Handlers;

internal class PlatformHandler : DisposableObject
{
    private readonly IWindow window;
    private readonly IInputContext input;
    private readonly Lock @lock = new();

    private List<PlatformInput> pendingInputs = new(64);
    private List<PlatformInput> frameInputs = new(64);
    private Vector2 dpiScale = Vector2.One;
    private string clipboardText = string.Empty;
    private string? pendingClipboardText;
    private StandardCursor cursor = StandardCursor.Default;
    private CursorMode cursorMode = CursorMode.Normal;
    private bool hasPendingCursor;

    public PlatformHandler(IWindow window)
    {
        this.window = window;
        input = window.CreateInput();

        foreach (IMouse mouse in input.Mice)
        {
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnMouseScroll;
        }

        foreach (IKeyboard keyboard in input.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
            keyboard.KeyChar += OnKeyChar;
        }

        window.FocusChanged += OnFocusChanged;
        window.Update += OnUpdate;

        OnUpdate(0.0);
    }

    public Vector2 DpiScale
    {
        get
        {
            using Lock.Scope _ = @lock.EnterScope();

            return dpiScale;
        }
    }

    public string ClipboardText
    {
        get
        {
            using Lock.Scope _ = @lock.EnterScope();

            return pendingClipboardText ?? clipboardText;
        }
        set
        {
            using Lock.Scope _ = @lock.EnterScope();

            pendingClipboardText = value;
        }
    }

    public ReadOnlySpan<PlatformInput> CollectInput()
    {
        frameInputs.Clear();

        using Lock.Scope _ = @lock.EnterScope();

        (frameInputs, pendingInputs) = (pendingInputs, frameInputs);

        return CollectionsMarshal.AsSpan(frameInputs);
    }

    public void SetCursor(StandardCursor shape, CursorMode mode)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (cursor == shape && cursorMode == mode)
        {
            return;
        }

        cursor = shape;
        cursorMode = mode;
        hasPendingCursor = true;
    }

    protected override void Destroy()
    {
        if (window is not null)
        {
            window.Update -= OnUpdate;
            window.FocusChanged -= OnFocusChanged;
        }

        if (input is not null)
        {
            foreach (IKeyboard keyboard in input.Keyboards)
            {
                keyboard.KeyChar -= OnKeyChar;
                keyboard.KeyUp -= OnKeyUp;
                keyboard.KeyDown -= OnKeyDown;
            }

            foreach (IMouse mouse in input.Mice)
            {
                mouse.Scroll -= OnMouseScroll;
                mouse.MouseMove -= OnMouseMove;
                mouse.MouseUp -= OnMouseUp;
                mouse.MouseDown -= OnMouseDown;
            }

            input.Dispose();
        }
    }

    private void OnUpdate(double deltaSeconds)
    {
        string? text;
        StandardCursor shape;
        CursorMode mode;
        bool updateCursor;

        {
            using Lock.Scope _ = @lock.EnterScope();

            text = pendingClipboardText;
            pendingClipboardText = null;
            shape = cursor;
            mode = cursorMode;
            updateCursor = hasPendingCursor;
            hasPendingCursor = false;
        }

        if (text is not null)
        {
            input.Keyboards[0].ClipboardText = text;
        }

        if (updateCursor)
        {
            foreach (IMouse mouse in input.Mice)
            {
                mouse.Cursor.StandardCursor = shape;
                mouse.Cursor.CursorMode = mode;
            }
        }

        Vector2 scale = DpiScale;

        if (window.Size.X > 0 && window.Size.Y > 0 && window.FramebufferSize.X > 0 && window.FramebufferSize.Y > 0)
        {
            scale = (Vector2)window.FramebufferSize / (Vector2)window.Size;
        }

        string clipboard = input.Keyboards[0].ClipboardText;

        {
            using Lock.Scope _ = @lock.EnterScope();

            dpiScale = scale;
            clipboardText = clipboard;
        }
    }

    private void AddInput(PlatformInput message)
    {
        using Lock.Scope _ = @lock.EnterScope();

        pendingInputs.Add(message);
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        AddInput(new()
        {
            Type = PlatformInputType.MouseDown,
            Button = button,
            Position = mouse.Position
        });
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        AddInput(new()
        {
            Type = PlatformInputType.MouseUp,
            Button = button,
            Position = mouse.Position
        });
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        AddInput(new()
        {
            Type = PlatformInputType.MouseMove,
            Position = position
        });
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel offset)
    {
        AddInput(new()
        {
            Type = PlatformInputType.MouseWheel,
            Offset = new(offset.X, offset.Y)
        });
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        AddInput(new()
        {
            Type = PlatformInputType.KeyDown,
            Key = key
        });
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        AddInput(new()
        {
            Type = PlatformInputType.KeyUp,
            Key = key
        });
    }

    private void OnKeyChar(IKeyboard keyboard, char character)
    {
        AddInput(new()
        {
            Type = PlatformInputType.TextInput,
            Character = character
        });
    }

    private void OnFocusChanged(bool isFocused)
    {
        if (!isFocused)
        {
            AddInput(new() { Type = PlatformInputType.FocusLost });
        }
    }
}
