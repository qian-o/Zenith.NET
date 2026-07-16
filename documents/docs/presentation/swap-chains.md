# Surfaces and Swap Chains

A `Surface` connects Zenith.NET to a native window. A `SwapChain` owns the presentation images for that surface and exposes the current image through `Drawable`.

Presentation is synchronous. Submit and wait before calling `Present`; swap-chain frames in flight are not supported.

## Creating a Surface

Create the surface that matches the native window system:

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

Zenith.NET provides constructors for Win32, Wayland, Xlib, Android, and Apple surfaces. The application or UI integration is responsible for obtaining the required native handles.

## Creating a Swap Chain

Create the swap chain through the active `GraphicsContext`:

```csharp
SwapChain swapChain = context.CreateSwapChain(new()
{
    Surface = surface,
    Format = PixelFormat.B8G8R8A8UNorm
});
```

Choose a format supported by the application pipeline. Graphics pipelines that render directly to the drawable must declare the same color format.

## Rendering a Frame

Transition the current drawable before attachment access and again before presentation:

```csharp
CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();
Texture drawable = swapChain.Drawable;

commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);

commandBuffer.BeginRenderPass([ColorAttachment.Clear(drawable, new(0.05f, 0.05f, 0.08f, 1.0f))], null);

commandBuffer.SetPipeline(graphicsPipeline);
commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
commandBuffer.Draw(vertexCount, 1, 0, 0);

commandBuffer.EndRenderPass();
commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);

commandBuffer.Submit().Wait();
swapChain.Present();
```

Acquire `Drawable` for each frame because the current presentation image changes.

This direct swap-chain flow owns its command buffer. View integrations instead supply a View-owned command buffer that subscribers may only record into.

## Resizing

Skip rendering while either drawable dimension is zero. Resize size-dependent render targets before resizing the swap chain:

```csharp
if (width is 0 || height is 0)
{
    return;
}

color.Dispose();
depthStencil.Dispose();

color = context.CreateTexture(TextureDesc.ColorAttachment(PixelFormat.B8G8R8A8UNorm, width, height, 1, SampleCount.Count1));

depthStencil = context.CreateTexture(TextureDesc.DepthStencilAttachment(PixelFormat.D32FloatS8UInt, width, height, SampleCount.Count1));

swapChain.Resize(width, height);
```

Recreate pipelines only when their declared `AttachmentFormats` change. A size change alone does not change the pipeline's attachment format contract.

## Refreshing Native Handles

Some UI frameworks recreate their native surface without changing the logical view. Build a new `Surface` and call `Refresh` when the underlying native handle changes:

```csharp
Surface surface = Surface.Win32(hwnd, width, height);
swapChain.Refresh(surface);
```

Use `Resize` when only the dimensions change. Use `Refresh` when the native handle or surface type changes.

