# Views

Zenith.NET provides controls for supported .NET UI frameworks. Each control implements `IZenithView` and manages its own frame presentation.

## Framework Packages

| Package | Framework |
|---|---|
| `Zenith.NET.Views.WinForms` | Windows Forms |
| `Zenith.NET.Views.WPF` | WPF |
| `Zenith.NET.Views.WinUI` | WinUI 3 and Uno |
| `Zenith.NET.Views.Maui` | .NET MAUI |
| `Zenith.NET.Views.Avalonia` | Avalonia |

Add the package for the framework used by the application.

## Connect a View

Assign a `GraphicsContext`, then subscribe to updates and rendering:

```csharp
using Zenith.NET;
using Zenith.NET.Views;

zenithView.GraphicsContext = context;

zenithView.UpdateRequested += (_, args) => simulation.Update(args.DeltaSeconds);

zenithView.RenderRequested += (_, args) =>
{
    args.CommandBuffer.BeginRenderPass([ColorAttachment.Clear(args.Drawable, clearColor)], null);

    args.CommandBuffer.SetPipeline(pipeline);
    args.CommandBuffer.Draw(vertexCount, 1, 0, 0);

    args.CommandBuffer.EndRenderPass();
};
```

Both event argument types provide `DeltaSeconds` and `TotalSeconds`. `RenderEventArgs` also provides the current `CommandBuffer` and `Drawable`.

## Follow the Callback Contract

The command buffer and drawable are borrowed for the duration of the synchronous `RenderRequested` callback. Within the callback:

- Record commands into `args.CommandBuffer`.
- Treat `args.Drawable` as a `ColorAttachment`.
- End every render pass before returning.
- Do not submit, wait, dispose, or retain either object.

The View completes and presents the frame after the callback returns. Dispose application-owned pipelines and resources separately, and keep the assigned context alive until the control has released its rendering resources.

For graphics API selection, see [Runtime](../fundamentals/runtime.md). Use [Swap Chains](swap-chains.md) when the application manages a window without a View integration.
