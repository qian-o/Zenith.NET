using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using HexaImGui = Hexa.NET.ImGui.ImGui;
using HexaImGuiContext = Hexa.NET.ImGui.ImGuiContext;

namespace Zenith.NET.Extensions.ImGui;

internal unsafe partial class ImGuiContext : DisposableObject
{
    private readonly ImGuiContextPtr imGui;
    private readonly PlatformGetClipboardTextFn platformGetClipboardText;
    private readonly PlatformSetClipboardTextFn platformSetClipboardText;
    private readonly PlatformSetImeDataFn platformSetImeData;
    private readonly GCHandle platformGetClipboardTextHandle;
    private readonly GCHandle platformSetClipboardTextHandle;
    private readonly GCHandle platformSetImeDataHandle;
    private readonly List<ImGuiInputArgs> inputs = [];

    private bool frameBegun;
    private ZenithMarshal.Scope? clipboardScope;

    internal ImGuiContext(GraphicsContext context, IImGuiPlatform platform, AttachmentFormats attachmentFormats, ImGuiColorSpace colorSpace)
    {
        Context = context;
        Platform = platform;
        Platform.Input += OnInput;

        HexaImGui.SetCurrentContext(imGui = HexaImGui.CreateContext());

        ImGuiIOPtr io = HexaImGui.GetIO();

        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;

        Platform.Initialize(io);

        ImGuiPlatformIOPtr platformIo = HexaImGui.GetPlatformIO();

        platformGetClipboardTextHandle = GCHandle.Alloc(platformGetClipboardText = PlatformGetClipboardText);
        platformSetClipboardTextHandle = GCHandle.Alloc(platformSetClipboardText = PlatformSetClipboardText);
        platformSetImeDataHandle = GCHandle.Alloc(platformSetImeData = PlatformSetImeData);

        platformIo.PlatformGetClipboardTextFn = (void*)Marshal.GetFunctionPointerForDelegate(platformGetClipboardText);
        platformIo.PlatformSetClipboardTextFn = (void*)Marshal.GetFunctionPointerForDelegate(platformSetClipboardText);
        platformIo.PlatformSetImeDataFn = (void*)Marshal.GetFunctionPointerForDelegate(platformSetImeData);

        InitializeGraphics(attachmentFormats, colorSpace);
    }

    internal GraphicsContext Context { get; }

    internal IImGuiPlatform Platform { get; }

    public void Update(double delta, uint width, uint height)
    {
        HexaImGui.SetCurrentContext(imGui);

        if (frameBegun)
        {
            HexaImGui.Render();
        }

        ImGuiIOPtr io = HexaImGui.GetIO();

        io.DeltaTime = (float)delta;
        io.DisplaySize.X = width;
        io.DisplaySize.Y = height;

        Platform.SetCursor(HexaImGui.GetMouseCursor());

        foreach (ImGuiInputArgs input in inputs)
        {
            switch (input.Type)
            {
                case ImGuiInput.MouseDown:
                    io.AddMouseButtonEvent((int)input.MouseButton, true);
                    break;

                case ImGuiInput.MouseUp:
                    io.AddMouseButtonEvent((int)input.MouseButton, false);
                    break;

                case ImGuiInput.MouseMove:
                    io.AddMousePosEvent(input.Position.X, input.Position.Y);
                    break;

                case ImGuiInput.MouseWheel:
                    io.AddMouseWheelEvent(input.Offset.X, input.Offset.Y);
                    break;

                case ImGuiInput.KeyDown:
                    io.AddKeyEvent(input.Key, true);
                    break;

                case ImGuiInput.KeyUp:
                    io.AddKeyEvent(input.Key, false);
                    break;

                case ImGuiInput.TextInput:
                    io.AddInputCharacter(input.Character);
                    break;
            }
        }
        inputs.Clear();

        HexaImGui.NewFrame();

        frameBegun = true;
    }

    public void Render(CommandBuffer commandBuffer, ColorAttachment colorAttachment)
    {
        HexaImGui.SetCurrentContext(imGui);

        if (frameBegun)
        {
            HexaImGui.Render();

            Render(commandBuffer, colorAttachment, HexaImGui.GetDrawData());

            frameBegun = false;
        }
    }

    protected override void Destroy()
    {
        DestroyGraphics();

        clipboardScope?.Dispose();

        inputs.Clear();
        platformSetImeDataHandle.Free();
        platformSetClipboardTextHandle.Free();
        platformGetClipboardTextHandle.Free();

        HexaImGui.SetCurrentContext(null);
        HexaImGui.DestroyContext(imGui);

        Platform.Input -= OnInput;
    }

    private void OnInput(object? sender, ImGuiInputArgs args)
    {
        inputs.Add(args);
    }

    private byte* PlatformGetClipboardText(HexaImGuiContext* context)
    {
        clipboardScope?.Dispose();
        clipboardScope = new();

        return (byte*)ZenithMarshal.StringToPointer(clipboardScope, Platform.GetClipboardText(), StringEncoding.UTF8);
    }

    private void PlatformSetClipboardText(HexaImGuiContext* context, byte* text)
    {
        Platform.SetClipboardText(ZenithMarshal.StringFromPointer((nint)text, StringEncoding.UTF8));
    }

    private void PlatformSetImeData(HexaImGuiContext* context, ImGuiViewport* viewport, ImGuiPlatformImeData* data)
    {
        Platform.SetImeData((ImGuiViewportPtr)viewport, (ImGuiPlatformImeDataPtr)data);
    }
}
