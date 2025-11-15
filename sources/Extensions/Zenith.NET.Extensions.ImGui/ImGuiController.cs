using System.Numerics;
using Hexa.NET.ImGui;
using HexaImGui = Hexa.NET.ImGui.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public unsafe class ImGuiController : DisposableObject
{
    private readonly ImGuiRenderer renderer;
    private readonly List<ImGuiMouseButton> mouseDowns = [];
    private readonly List<ImGuiMouseButton> mouseUps = [];
    private readonly List<Vector2> mouseMoves = [];
    private readonly List<Vector2> mouseWheels = [];
    private readonly List<ImGuiKey> keyDowns = [];
    private readonly List<ImGuiKey> keyUps = [];
    private readonly List<char> chars = [];

    private bool frameBegun;

    public ImGuiController(GraphicsContext context, Output output, ImGuiColorSpace colorSpace, string? fontPath = null, Action<ImGuiIOPtr>? otherSetup = null)
    {
        HexaImGui.SetCurrentContext(Context = HexaImGui.GetCurrentContext());

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

        renderer = new(context, output, colorSpace);
    }

    public ImGuiContextPtr Context { get; }

    public Action<ImGuiMouseCursor>? SetCursor { get; set; }

    public ImTextureRef Binding(Texture texture)
    {
        return new(null, renderer.Binding(texture));
    }

    public ImTextureRef Binding(TextureView textureView)
    {
        return new(null, renderer.Binding(textureView));
    }

    public void Update(double delta, uint width, uint height)
    {
        if (Context.IsNull)
        {
            return;
        }

        HexaImGui.SetCurrentContext(Context);

        if (frameBegun)
        {
            HexaImGui.Render();
        }

        ImGuiIOPtr io = HexaImGui.GetIO();

        io.DeltaTime = (float)delta;
        io.DisplaySize.X = width;
        io.DisplaySize.Y = height;

        SetCursor?.Invoke(HexaImGui.GetMouseCursor());

        foreach (ImGuiMouseButton button in mouseDowns)
        {
            io.AddMouseButtonEvent((int)button, true);
        }

        foreach (ImGuiMouseButton button in mouseUps)
        {
            io.AddMouseButtonEvent((int)button, false);
        }

        foreach (Vector2 position in mouseMoves)
        {
            io.AddMousePosEvent(position.X, position.Y);
        }

        foreach (Vector2 offset in mouseWheels)
        {
            io.AddMouseWheelEvent(offset.X, offset.Y);
        }

        foreach (ImGuiKey key in keyDowns)
        {
            io.AddKeyEvent(key, true);
        }

        foreach (ImGuiKey key in keyUps)
        {
            io.AddKeyEvent(key, false);
        }

        foreach (char c in chars)
        {
            io.AddInputCharacter(c);
        }

        mouseDowns.Clear();
        mouseUps.Clear();
        mouseMoves.Clear();
        mouseWheels.Clear();
        keyDowns.Clear();
        keyUps.Clear();
        chars.Clear();

        HexaImGui.NewFrame();
    }

    public void Render(CommandBuffer commandBuffer)
    {
        if (Context.IsNull)
        {
            return;
        }

        HexaImGui.SetCurrentContext(Context);

        if (frameBegun)
        {
            HexaImGui.Render();

            renderer.Render(commandBuffer, HexaImGui.GetDrawData());

            frameBegun = false;
        }
    }

    public void MouseDown(ImGuiMouseButton button)
    {
        mouseDowns.Add(button);
    }

    public void MouseUp(ImGuiMouseButton button)
    {
        mouseUps.Add(button);
    }

    public void MouseMove(Vector2 position)
    {
        mouseMoves.Add(position);
    }

    public void MouseWheel(Vector2 offset)
    {
        mouseWheels.Add(offset);
    }

    public void KeyDown(ImGuiKey key)
    {
        keyDowns.Add(key);
    }

    public void KeyUp(ImGuiKey key)
    {
        keyUps.Add(key);
    }

    public void AddChar(char c)
    {
        chars.Add(c);
    }

    protected override void Destroy()
    {
        if (Context.IsNull)
        {
            return;
        }

        renderer.Dispose();

        HexaImGui.SetCurrentContext(null);
        HexaImGui.DestroyContext(Context);
    }
}
