using System.Numerics;
using Silk.NET.Input;

namespace CornellBox.Handlers;

internal struct PlatformInput
{
    public PlatformInputType Type;

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

    TextInput,

    FocusLost
}
