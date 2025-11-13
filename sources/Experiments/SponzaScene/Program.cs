using Silk.NET.Windowing;
using Zenith.NET;

WindowOptions options = WindowOptions.Default;
options.API = GraphicsAPI.None;

IWindow window = Window.Create(options);
window.Initialize();

GraphicsContext context = GraphicsContext.CreateVulkan(true);
context.ValidationMessage += (sender, args) => Console.WriteLine($"[{args.Source} - {args.Severity}] {args.Message}");

Surface surface;
if (OperatingSystem.IsWindows())
{
    surface = Surface.Win32(window.Native!.Win32!.Value.Hwnd, (uint)window.Size.X, (uint)window.Size.Y);
}
else if (OperatingSystem.IsLinux())
{
    surface = Surface.Xlib(window.Native!.X11!.Value.Display, (nint)window.Native.X11.Value.Window, (uint)window.Size.X, (uint)window.Size.Y);
}
else
{
    return;
}

SwapChain swapChain = context.CreateSwapChain(new()
{
    Surface = surface,
    ColorTargetFormat = PixelFormat.R8G8B8A8UNorm,
    DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
});

window.Resize += size => swapChain.Resize((uint)size.X, (uint)size.Y);
window.Render += delta =>
{
    CommandBuffer commandBuffer = context.Graphics.CommandBuffer();

    commandBuffer.BindFrameBuffer(swapChain.FrameBuffer, ClearValues.Default);
    commandBuffer.Submit();

    swapChain.Present();

    context.Graphics.WaitIdle();
};

window.Run();