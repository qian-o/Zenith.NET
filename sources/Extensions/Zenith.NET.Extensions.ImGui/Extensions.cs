using Hexa.NET.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public static class Extensions
{
    extension(Texture texture)
    {
        public ImTextureRef ImGuiBinding => ImGuiController.Current?.Binding(texture) ?? default;
    }

    extension(TextureView textureView)
    {
        public ImTextureRef ImGuiBinding => ImGuiController.Current?.Binding(textureView) ?? default;
    }
}
