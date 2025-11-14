using System.Numerics;
using Hexa.NET.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public interface IImGuiInput
{
    ImGuiMouseCursor Cursor { get; set; }

    event Action<ImGuiMouseButton> OnMouseDown;

    event Action<ImGuiMouseButton> OnMouseUp;

    event Action<Vector2> OnMouseMove;

    event Action<Vector2> OnMouseWheel;

    event Action<ImGuiKey> OnKeyDown;

    event Action<ImGuiKey> OnKeyUp;

    event Action<char> OnCharInput;
}
