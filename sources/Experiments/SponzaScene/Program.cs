using Silk.NET.Windowing;
using SponzaScene;
using Zenith.NET;
using Zenith.NET.Extensions.ImGui;
using Zenith.NET.Vulkan;

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

SwapChain swapChain = context.CreateSwapChain(new() { Surface = surface, ColorTargetFormat = PixelFormat.R8G8B8A8UNorm, DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt });

ImGuiController imGuiController = new(context, swapChain.FrameBuffer.Output, ImGuiColorSpace.Legacy);

MainView mainView = new(context);

window.Update += delta =>
{
    imGuiController.Update(delta, (uint)window.Size.X, (uint)window.Size.Y);

    mainView.Update(delta);

    Dispatcher.Process();
};

window.Render += delta =>
{
    if (window.Size.X is 0 || window.Size.Y is 0)
    {
        return;
    }

    mainView.Render(delta);

    CommandBuffer commandBuffer = context.Graphics.CommandBuffer();

    commandBuffer.BindFrameBuffer(swapChain.FrameBuffer, ClearValues.Default);

    imGuiController.Render(commandBuffer);

    commandBuffer.Submit();

    context.Copy.WaitIdle();
    context.Compute.WaitIdle();
    context.Graphics.WaitIdle();

    swapChain.Present();
};

window.Resize += size =>
{
    if (size.X is 0 || size.Y is 0)
    {
        return;
    }

    swapChain.Resize((uint)size.X, (uint)size.Y);
};

window.Run();