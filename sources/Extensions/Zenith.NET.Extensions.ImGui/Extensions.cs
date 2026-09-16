using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using HexaImGui = Hexa.NET.ImGui.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public static class Extensions
{
    extension(Texture texture)
    {
        public ImTextureRef ImGuiBinding => Controller().Binding(texture);
    }

    extension(TextureView textureView)
    {
        public ImTextureRef ImGuiBinding => Controller().Binding(textureView);
    }

    private static unsafe ImGuiController Controller()
    {
        return (ImGuiController)GCHandle.FromIntPtr((nint)HexaImGui.GetIO().BackendRendererUserData).Target!;
    }
}
