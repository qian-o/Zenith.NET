# Swap Chains

A `Surface` identifies a window and its drawable size. `SwapChain.Drawable` returns the texture to render for the current frame.

## Create a Surface

Create the surface that matches the application's window system and native handles. For example:

```csharp
Surface surface = Surface.Win32(hwnd, width, height);
```

Zenith.NET provides `Win32`, `Wayland`, `Xlib`, `Android`, and `Apple` surface factories. Select the factory that corresponds to the handles supplied by the window host. Use a [View integration](views.md) when a supported UI framework should manage the surface.

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

Complete the rendering submission before calling `Present()`. Do not dispose the drawable or retain it for another frame; request `Drawable` again after presentation.

## Resize

Skip rendering while either dimension is zero. Recreate size-dependent textures and resize the swap chain before rendering again:

```csharp
if (width is 0 || height is 0)
{
    return;
}

color.Dispose();
depthStencil.Dispose();

TextureDesc colorDesc = TextureDesc.ColorAttachment(PixelFormat.B8G8R8A8UNorm,
                                                    width,
                                                    height,
                                                    1,
                                                    SampleCount.Count1);
TextureDesc depthDesc = TextureDesc.DepthStencilAttachment(PixelFormat.D32FloatS8UInt,
                                                           width,
                                                           height,
                                                           SampleCount.Count1);

color = context.CreateTexture(colorDesc);
depthStencil = context.CreateTexture(depthDesc);

swapChain.Resize(width, height);
```

Recreate a pipeline only when its attachment formats or sample count change.

## Refresh a Surface

If the window system replaces its surface handle, create a new `Surface` and refresh the swap chain:

```csharp
swapChain.Refresh(Surface.Win32(hwnd, width, height));
```

Use `Resize` when only the dimensions change. Use `Refresh` when the handle or surface type changes.

