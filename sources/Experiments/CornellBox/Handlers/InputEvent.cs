using System.Numerics;
using Silk.NET.Input;

namespace CornellBox.Handlers;

internal struct InputEvent
{
    public InputEventType Type;

    public MouseButton MouseButton;

    public Vector2 Position;

    public Vector2 Offset;

    public Key Key;

    public char Character;
}
