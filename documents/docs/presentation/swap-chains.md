# Swap Chains

A `Surface` identifies a window and its drawable size. A `SwapChain` provides the texture rendered for the next presentation.

## Create a Surface

Create the surface that matches the application's window system:

```csharp
Surface surface;
if (OperatingSystem.IsWindows())
{
    surface = Surface.Win32(hwnd, width, height);
}
else if (OperatingSystem.IsMacOS())
{
    surface = Surface.Apple(layer, width, height);
}
else
{
    surface = Surface.Xlib(display, window, width, height);
}
```

Zenith.NET provides `Win32`, `Wayland`, `Xlib`, `Android`, and `Apple` surface factories. Use a [View integration](views.md) when a supported UI framework should manage the surface.

## Create a Swap Chain

Create the swap chain from the same context used for rendering:

```csharp
using SwapChain swapChain = context.CreateSwapChain(new()
{
    Surface = surface,
    Format = PixelFormat.B8G8R8A8UNorm
});
```

The graphics pipeline color format must match the swap-chain format.

## Render and Present

Get the current drawable for each frame, transition it for rendering, then transition it for presentation:

```csharp
CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();
Texture drawable = swapChain.Drawable;

commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
commandBuffer.BeginRenderPass([ColorAttachment.Clear(drawable, clearColor)], null);

commandBuffer.SetPipeline(graphicsPipeline);
commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
commandBuffer.Draw(vertexCount, 1, 0, 0);

commandBuffer.EndRenderPass();
commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);

commandBuffer.Submit().Wait();
swapChain.Present();
```

Presentation is synchronous. Wait for the rendering submission before calling `Present()`. Request `Drawable` again for the next frame.

## Resize

Skip rendering while either dimension is zero. Recreate size-dependent textures and resize the swap chain before rendering again:

```csharp
if (width is 0 || height is 0)
{
    return;
}

color.Dispose();
depthStencil.Dispose();

color = context.CreateTexture(
    TextureDesc.ColorAttachment(PixelFormat.B8G8R8A8UNorm, width, height, 1, SampleCount.Count1));
depthStencil = context.CreateTexture(
    TextureDesc.DepthStencilAttachment(PixelFormat.D32FloatS8UInt, width, height, SampleCount.Count1));

swapChain.Resize(width, height);
```

Recreate a pipeline only when its attachment formats or sample count change.

## Refresh a Surface

If the window system replaces its surface handle, create a new `Surface` and refresh the swap chain:

```csharp
swapChain.Refresh(Surface.Win32(hwnd, width, height));
```

Use `Resize` when only the dimensions change. Use `Refresh` when the handle or surface type changes.

