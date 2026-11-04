using Hexa.NET.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public interface IImGuiPlatform
{
    string ClipboardText { get; set; }

    event EventHandler<ImGuiInputArgs>? Input;

    void Initialize(ImGuiIOPtr io);

    void SetCursor(ImGuiMouseCursor cursor);

    void SetImeData(ImGuiViewportPtr viewport, ImGuiPlatformImeDataPtr data);
}
