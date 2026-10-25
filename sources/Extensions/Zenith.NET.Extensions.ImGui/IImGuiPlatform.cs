using Hexa.NET.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public interface IImGuiPlatform
{
    event EventHandler<ImGuiInputArgs>? Input;

    void Initialize(ImGuiIOPtr io);

    void SetCursor(ImGuiMouseCursor cursor);

    string GetClipboardText();

    void SetClipboardText(string text);

    void SetImeData(ImGuiViewportPtr viewport, ImGuiPlatformImeDataPtr data);
}
