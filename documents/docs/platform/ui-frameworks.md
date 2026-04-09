# UI Framework Integration

Zenith.NET provides view controls for major .NET UI frameworks through the `Zenith.NET.Views.*` packages.

## IZenithView

All view implementations share the `IZenithView` interface:

| Member | Description |
|--------|-------------|
| `GraphicsContext` | The graphics context used for rendering (assign before use) |
| `UpdateRequested` | Event fired each frame for logic updates |
| `RenderRequested` | Event fired each frame for GPU rendering |
| `UI(Action)` | Execute an action on the UI thread |
| `EnsureResources()` | Create/recreate swap chain and frame buffer resources |
| `Tick()` | Drive one frame update + render cycle |
| `Present()` | Present the rendered frame |
| `ReleaseResources()` | Dispose GPU resources |

### Event Args

| Type | Properties |
|------|-----------|
| `UpdateEventArgs` | `DeltaSeconds`, `TotalSeconds` |
| `RenderEventArgs` | `DeltaSeconds`, `TotalSeconds`, `FrameBuffer` |

## Supported Frameworks

| Package | Framework | Platforms |
|---------|-----------|-----------|
| `Zenith.NET.Views.Avalonia` | Avalonia | Windows, macOS, Linux |
| `Zenith.NET.Views.Maui` | .NET MAUI | Windows, macOS, Android, iOS |
| `Zenith.NET.Views.WinForms` | Windows Forms | Windows |
| `Zenith.NET.Views.WinUI` | WinUI 3 / Uno Platform | Windows (+ Uno targets) |
| `Zenith.NET.Views.WPF` | WPF | Windows |

## Basic Usage

All views follow the same pattern:

```csharp
// 1. Create a graphics context
GraphicsContext context = GraphicsContext.CreateDirectX12(useValidationLayer: false);

// 2. Assign to the view
zenithView.GraphicsContext = context;

// 3. Subscribe to events
zenithView.UpdateRequested += (sender, args) =>
{
    // Update logic (animations, input, etc.)
};

zenithView.RenderRequested += (sender, args) =>
{
    CommandBuffer cmd = context.Graphics.CommandBuffer();
    cmd.BeginRenderPass(args.FrameBuffer, clearValue);
    // ... draw commands ...
    cmd.EndRenderPass();
    cmd.Submit(waitForCompletion: true);
};
```

## ZenithViewHelper

`ZenithViewHelper` provides shared defaults for all view implementations:

| Property | Value | Description |
|----------|-------|-------------|
| `ColorFormat` | `B8G8R8A8UNorm` (desktop) / `R8G8B8A8UNorm` (Android) | Default swap chain color format |
| `DepthStencilFormat` | `D32FloatS8UInt` | Default depth/stencil format |
| `Output` | Derived from above | Pre-configured `Output` for pipeline creation |

## FrameScheduler

`FrameScheduler` drives the render loop for views that don't have a built-in frame tick mechanism. It calls `Tick()` and `Present()` on the view at the appropriate cadence.
