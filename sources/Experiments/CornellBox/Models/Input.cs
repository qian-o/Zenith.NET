using System.Numerics;
using Silk.NET.Input;

namespace CornellBox.Models;

internal struct Input(InputType type)
{
    public InputType Type = type;

    public Key Key;

    public MouseButton Button;

    public Vector2 Position;

    public Vector2 Offset;

    public char Character;
}
