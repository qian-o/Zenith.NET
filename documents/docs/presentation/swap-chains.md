# Swap Chains

A `Surface` identifies a window and its drawable size. `SwapChain.Drawable` returns the texture to render for the current frame.

## Create a Surface

Create the surface that matches the application's window system and native handles, such as `Surface.Win32(hwnd, width, height)`.

Zenith.NET provides `Win32`, `Wayland`, `Xlib`, `Android`, and `Apple` surface factories. Select the factory that corresponds to the handles supplied by the window host. Use a [View integration](views.md) when a supported UI framework should manage the surface.

## Create a Swap Chain

Create the swap chain from the same context used for rendering:

```csharp
SwapChain swapChain = context.CreateSwapChain(new()
{
    Surface = surface,
    Format = PixelFormat.B8G8R8A8UNorm
});
```

The graphics pipeline color format must match the swap-chain format.

## Render and Present

Borrow a graphics command buffer for each frame, record the current drawable, and wait for the submission to complete before presentation:

```csharp
CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();

commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
commandBuffer.BeginRenderPass([ColorAttachment.Clear(swapChain.Drawable, default)], null);

commandBuffer.SetPipeline(pipeline);
commandBuffer.SetVertexBuffer(buffer, 0, 0);
commandBuffer.Draw(vertexCount, 1, 0, 0);

commandBuffer.EndRenderPass();
commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);

commandBuffer.Submit().Wait();
swapChain.Present();
```

Wait for the graphics submission before calling `Present()`. Do not dispose or retain the command buffer or drawable; borrow both again for the next frame.

## Resize

Skip rendering while either dimension is zero. Dispose and recreate application-owned size-dependent textures, then call `swapChain.Resize(width, height)` before rendering again.

Recreate a pipeline only when its attachment formats or sample count change.

## Refresh a Surface

If the window system replaces its surface handle, create a new `Surface` and pass it to `swapChain.Refresh(...)`.

Use `Resize` when only the dimensions change. Use `Refresh` when the handle or surface type changes.
