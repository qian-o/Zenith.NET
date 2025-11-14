using Hexa.NET.ImGui;
using HexaImGui = Hexa.NET.ImGui.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public unsafe class ImGuiController : DisposableObject
{
    public ImGuiController(GraphicsContext context,
                           IImGuiInput input,
                           Output output,
                           ImGuiColorSpace colorSpace = ImGuiColorSpace.Legacy,
                           string? fontPath = null,
                           Action<ImGuiIOPtr>? otherSetup = null)
    {
        HexaImGui.SetCurrentContext(Handle = HexaImGui.CreateContext());

        ImGuiIOPtr io = HexaImGui.GetIO();

        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;

        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;

        if (fontPath is not null)
        {
            io.Fonts.Clear();

            io.Fonts.AddFontFromFileTTF(fontPath);
        }

        otherSetup?.Invoke(io);
    }

    public ImGuiContextPtr Handle { get; }

    protected override void Destroy()
    {
        HexaImGui.SetCurrentContext(null);
        HexaImGui.DestroyContext(Handle);
    }
}
