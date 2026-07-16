# View Integrations

Zenith.NET provides UI controls through `Zenith.NET.Views` and framework-specific `Zenith.NET.Views.*` packages. Every control implements `IZenithView`.

## View Contract

| Member | Purpose |
|---|---|
| `GraphicsContext` | Assigned by app code; rendering is skipped when null |
| `UpdateRequested` | Per-frame CPU update callback |
| `RenderRequested` | Per-frame callback with a command buffer and drawable texture |

`UpdateEventArgs` exposes `DeltaSeconds` and `TotalSeconds`. `RenderEventArgs` adds the View-owned `CommandBuffer` and current `Drawable` texture.

## Framework Packages

| Package | Framework |
|---|---|
| `Zenith.NET.Views.WinForms` | Windows Forms |
| `Zenith.NET.Views.WPF` | WPF |
| `Zenith.NET.Views.WinUI` | WinUI 3 and Uno |
| `Zenith.NET.Views.Maui` | .NET MAUI |
| `Zenith.NET.Views.Avalonia` | Avalonia |

Each integration handles its own presentation path.

## Render Callback

Assign a `GraphicsContext` and record rendering into the command buffer supplied by `RenderRequested`:

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
    args.CommandBuffer.BeginRenderPass([ColorAttachment.Clear(args.Drawable, new(0.05f, 0.05f, 0.08f, 1.0f))], null);
    // Record draw calls.
    args.CommandBuffer.EndRenderPass();
};
```

The command buffer and drawable are borrowed for the duration of the synchronous callback. Record commands only: do not submit, wait, dispose, or retain either object. The drawable enters and leaves the callback in `ColorAttachment`; the View performs the platform-specific final transition, submits, waits, and presents or copies the result.

For graphics API selection, see [Runtime and Devices](../fundamentals/runtime.md). For dependency rules, see [Synchronization](../fundamentals/synchronization.md).
