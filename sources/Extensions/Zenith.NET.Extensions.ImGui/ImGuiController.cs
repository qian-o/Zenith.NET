using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using HexaImGui = Hexa.NET.ImGui.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public unsafe class ImGuiController : DisposableObject
{
    private readonly ImGuiRenderer renderer;
    private readonly PlatformGetClipboardTextFn platformGetClipboardText;
    private readonly PlatformSetClipboardTextFn platformSetClipboardText;
    private readonly PlatformSetImeDataFn platformSetImeData;
    private readonly GCHandle platformGetClipboardTextHandle;
    private readonly GCHandle platformSetClipboardTextHandle;
    private readonly GCHandle platformSetImeDataHandle;
    private readonly List<ImGuiMouseButton> mouseDowns = [];
    private readonly List<ImGuiMouseButton> mouseUps = [];
    private readonly List<Vector2> mouseMoves = [];
    private readonly List<Vector2> mouseWheels = [];
    private readonly List<ImGuiKey> keyDowns = [];
    private readonly List<ImGuiKey> keyUps = [];
    private readonly List<char> keyChars = [];

    private bool frameBegun;
    private ZenithMarshal.Scope? clipboardScope;

    public ImGuiController(GraphicsContext context, Output output, ImGuiColorSpace colorSpace, string? fontPath = null, Action<ImGuiIOPtr>? otherSetup = null)
    {
        HexaImGui.SetCurrentContext(Context = HexaImGui.CreateContext());

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

        platformGetClipboardText = PlatformGetClipboardText;
        platformSetClipboardText = PlatformSetClipboardText;
        platformSetImeData = PlatformSetImeData;
        platformGetClipboardTextHandle = GCHandle.Alloc(platformGetClipboardText);
        platformSetClipboardTextHandle = GCHandle.Alloc(platformSetClipboardText);
        platformSetImeDataHandle = GCHandle.Alloc(platformSetImeData);

        ImGuiPlatformIOPtr platformIo = HexaImGui.GetPlatformIO();

        platformIo.PlatformGetClipboardTextFn = (void*)Marshal.GetFunctionPointerForDelegate(platformGetClipboardText);
        platformIo.PlatformSetClipboardTextFn = (void*)Marshal.GetFunctionPointerForDelegate(platformSetClipboardText);
        platformIo.PlatformSetImeDataFn = (void*)Marshal.GetFunctionPointerForDelegate(platformSetImeData);
    }

    public ImGuiContextPtr Context { get; }

    public IImGuiPlatformBindings? PlatformBindings { get; set; }

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
        HexaImGui.SetCurrentContext(Context);

        if (frameBegun)
        {
            HexaImGui.Render();
        }

        ImGuiIOPtr io = HexaImGui.GetIO();

        io.DeltaTime = (float)delta;
        io.DisplaySize.X = width;
        io.DisplaySize.Y = height;

        PlatformBindings?.SetCursor(HexaImGui.GetMouseCursor());

        foreach (ImGuiMouseButton button in mouseDowns)
        {
            io.AddMouseButtonEvent((int)button, true);
        }
        mouseDowns.Clear();

        foreach (ImGuiMouseButton button in mouseUps)
        {
            io.AddMouseButtonEvent((int)button, false);
        }
        mouseUps.Clear();

        foreach (Vector2 position in mouseMoves)
        {
            io.AddMousePosEvent(position.X, position.Y);
        }
        mouseMoves.Clear();

        foreach (Vector2 offset in mouseWheels)
        {
            io.AddMouseWheelEvent(offset.X, offset.Y);
        }
        mouseWheels.Clear();

        foreach (ImGuiKey key in keyDowns)
        {
            io.AddKeyEvent(key, true);
        }
        keyDowns.Clear();

        foreach (ImGuiKey key in keyUps)
        {
            io.AddKeyEvent(key, false);
        }
        keyUps.Clear();

        foreach (char c in keyChars)
        {
            io.AddInputCharacter(c);
        }
        keyChars.Clear();

        HexaImGui.NewFrame();

        frameBegun = true;
    }

    public void Render(CommandBuffer commandBuffer)
    {
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

    public void KeyChar(char c)
    {
        keyChars.Add(c);
    }

    protected override void Destroy()
    {
        clipboardScope?.Dispose();

        platformSetImeDataHandle.Free();
        platformSetClipboardTextHandle.Free();
        platformGetClipboardTextHandle.Free();
        renderer.Dispose();

        HexaImGui.SetCurrentContext(null);
        HexaImGui.DestroyContext(Context);
    }

    private byte* PlatformGetClipboardText(ImGuiContext* context)
    {
        clipboardScope?.Dispose();
        clipboardScope = new();

        return (byte*)ZenithMarshal.StringToPointer(clipboardScope, PlatformBindings?.GetClipboardText() ?? string.Empty, StringEncoding.UTF8);
    }

    private void PlatformSetClipboardText(ImGuiContext* context, byte* text)
    {
        PlatformBindings?.SetClipboardText(ZenithMarshal.StringFromPointer((nint)text, StringEncoding.UTF8));
    }

    private void PlatformSetImeData(ImGuiContext* context, ImGuiViewport* viewport, ImGuiPlatformImeData* data)
    {
        PlatformBindings?.SetImeData((ImGuiViewportPtr)viewport, (ImGuiPlatformImeDataPtr)data);
    }
}
