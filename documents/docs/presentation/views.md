# View Integrations

Zenith.NET provides UI controls through `Zenith.NET.Views` and framework-specific `Zenith.NET.Views.*` packages. Every control implements `IZenithView`.

## View Contract

| Member | Purpose |
|---|---|
| `GraphicsContext` | Assigned by app code; rendering is skipped when null |
| `UpdateRequested` | Per-frame CPU update callback |
| `RenderRequested` | Per-frame rendering callback with drawable texture |

`UpdateEventArgs` exposes `DeltaSeconds` and `TotalSeconds`. `RenderEventArgs` adds the current `Drawable` texture.

## Framework Paths

| Package | Framework | Final drawable layout |
|---|---|---|
| `Zenith.NET.Views.WinForms` | Windows Forms | `Present` |
| `Zenith.NET.Views.Maui` | MAUI on Android and Apple platforms | `Present` |
| `Zenith.NET.Views.WPF` | WPF | `ColorAttachment` |
| `Zenith.NET.Views.WinUI` | WinUI 3 and Uno | `ColorAttachment` |
| `Zenith.NET.Views.Maui` | MAUI on Windows | `ColorAttachment` |
| `Zenith.NET.Views.Avalonia` | Avalonia | `ColorAttachment` |

## Render Callback

Assign a `GraphicsContext` and render into `args.Drawable` from `RenderRequested`. This example applies to controls whose final layout is `Present`:

```csharp
using Zenith.NET;
using Zenith.NET.Views;

zenithView.GraphicsContext = context;

zenithView.UpdateRequested += (_, args) =>
{
    // Simulation and CPU-side updates.
};

zenithView.RenderRequested += (_, args) =>
{
    CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();

    commandBuffer.Transition(args.Drawable, default, TextureLayout.ColorAttachment);
    commandBuffer.BeginRenderPass([ColorAttachment.Load(args.Drawable)], null);
    // Record draw calls.
    commandBuffer.EndRenderPass();
    commandBuffer.Transition(args.Drawable, default, TextureLayout.Present);

    commandBuffer.Submit().Wait();
};
```

For controls whose final layout is `ColorAttachment`, end the pass in that layout. Submit and wait before the callback returns; the view presents after `RenderRequested` completes.

For graphics API selection, see [Runtime and Devices](../fundamentals/runtime.md). For dependency rules, see [Synchronization](../fundamentals/synchronization.md).
