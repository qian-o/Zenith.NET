using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Zenith.NET;

namespace CornellBox.Handlers;

internal class PlatformHandler : DisposableObject
{
    private readonly IInputContext input;
    private readonly IMouse mouse;
    private readonly IKeyboard keyboard;
    private readonly Lock @lock = new();

    private List<Input> pendingInputs = [];
    private List<Input> frameInputs = [];
    private volatile StandardCursor cursor;

    public PlatformHandler(IWindow window)
    {
        input = window.CreateInput();
        mouse = input.Mice[0];
        keyboard = input.Keyboards[0];

        mouse.MouseDown += (_, button) => AddInput(new(InputType.MouseDown)
        {
            Button = button,
            Position = mouse.Position
        });
        mouse.MouseUp += (_, button) => AddInput(new(InputType.MouseUp) { Button = button });
        mouse.MouseMove += (_, position) => AddInput(new(InputType.MouseMove) { Position = position });
        mouse.Scroll += (_, wheel) => AddInput(new(InputType.MouseWheel) { Offset = new(wheel.X, wheel.Y) });
        keyboard.KeyDown += (_, key, _) => AddInput(new(InputType.KeyDown) { Key = key });
        keyboard.KeyUp += (_, key, _) => AddInput(new(InputType.KeyUp) { Key = key });
        keyboard.KeyChar += (_, character) => AddInput(new(InputType.TextInput) { Character = character });

        window.Update += _ => mouse.Cursor.StandardCursor = cursor;

        DpiScale = (Vector2)window.FramebufferSize / (Vector2)window.Size;
    }

    public Vector2 DpiScale { get; }

    public string ClipboardText
    {
        get => keyboard.ClipboardText;
        set => keyboard.ClipboardText = value;
    }

    public StandardCursor Cursor
    {
        get => cursor;
        set => cursor = value;
    }

    public ReadOnlySpan<Input> CollectInput()
    {
        using Lock.Scope _ = @lock.EnterScope();

        (frameInputs, pendingInputs) = (pendingInputs, frameInputs);
        pendingInputs.Clear();

        return CollectionsMarshal.AsSpan(frameInputs);
    }

    protected override void Destroy()
    {
        input?.Dispose();
    }

    private void AddInput(Input message)
    {
        using Lock.Scope _ = @lock.EnterScope();

        pendingInputs.Add(message);
    }
}
