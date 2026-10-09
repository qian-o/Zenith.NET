using System.Numerics;
using Hexa.NET.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public class ImGuiInputArgs(ImGuiInput type) : EventArgs
{
    public ImGuiInput Type { get; } = type;

    public ImGuiMouseButton MouseButton { get; init; }

    public Vector2 Position { get; init; }

    public Vector2 Offset { get; init; }

    public ImGuiKey Key { get; init; }

    public char Character { get; init; }
}