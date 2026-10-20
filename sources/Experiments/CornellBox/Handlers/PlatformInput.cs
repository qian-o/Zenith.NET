using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Zenith.NET;

namespace CornellBox.Handlers;

internal class PlatformInput : DisposableObject
{
    private readonly IWindow window;
    private readonly IMouse mouse;
    private readonly IKeyboard keyboard;
    private readonly Lock @lock = new();
    private readonly List<InputEvent> collected = [];

    private List<InputEvent> pending = [];
    private List<InputEvent> drained = [];

    public PlatformInput(IWindow window)
    {
        this.window = window;
        Context = window.CreateInput();
        mouse = Context.Mice[0];
        keyboard = Context.Keyboards[0];

        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.MouseMove += OnMouseMove;
        mouse.Scroll += OnMouseScroll;
        keyboard.KeyDown += OnKeyDown;
        keyboard.KeyUp += OnKeyUp;
        keyboard.KeyChar += OnKeyChar;
        window.FocusChanged += OnFocusChanged;
    }

    public IInputContext Context { get; }

    public void Publish()
    {
        using Lock.Scope _ = @lock.EnterScope();

        pending.AddRange(collected);
        collected.Clear();
    }

    // Called only by the rendering thread. The returned batch is valid until the next Drain.
    public ReadOnlySpan<InputEvent> Drain()
    {
        using Lock.Scope _ = @lock.EnterScope();

        (pending, drained) = (drained, pending);
        pending.Clear();

        return CollectionsMarshal.AsSpan(drained);
    }

    public void Reset()
    {
        collected.Add(new() { Type = InputEventType.Reset });
    }

    protected override void Destroy()
    {
        if (window is not null)
        {
            window.FocusChanged -= OnFocusChanged;
        }

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

        Context?.Dispose();
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        collected.Add(new()
        {
            Type = InputEventType.MouseDown,
            MouseButton = button,
            Position = mouse.Position
        });
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        collected.Add(new()
        {
            Type = InputEventType.MouseUp,
            MouseButton = button
        });
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        collected.Add(new()
        {
            Type = InputEventType.MouseMove,
            Position = position
        });
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel offset)
    {
        collected.Add(new()
        {
            Type = InputEventType.MouseWheel,
            Offset = new(offset.X, offset.Y)
        });
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        collected.Add(new()
        {
            Type = InputEventType.KeyDown,
            Key = key
        });
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        collected.Add(new()
        {
            Type = InputEventType.KeyUp,
            Key = key
        });
    }

    private void OnKeyChar(IKeyboard keyboard, char character)
    {
        collected.Add(new()
        {
            Type = InputEventType.TextInput,
            Character = character
        });
    }

    private void OnFocusChanged(bool isFocused)
    {
        if (!isFocused)
        {
            Reset();
        }
    }
}
