using System.Numerics;
using Silk.NET.Input;

namespace CornellBox.Handlers;

internal struct PlatformInput(PlatformInputType type)
{
    public PlatformInputType Type = type;

    public Key Key;

    public MouseButton Button;

    public Vector2 Position;

    public Vector2 Offset;

    public char Character;
}

internal enum PlatformInputType
{
    MouseDown,

    MouseUp,

    MouseMove,

    MouseWheel,

    KeyDown,

    KeyUp,

    TextInput
}
