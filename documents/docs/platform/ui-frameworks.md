# UI Framework Integration

Zenith.NET provides UI-facing controls through `Zenith.NET.Views` and per-framework packages in `Zenith.NET.Views.*`.

The shared integration contract is `IZenithView`.

## Shared Abstractions

All framework views implement the same public surface:

| Member | Purpose |
|---|---|
| `GraphicsContext` | Assigned by app code; rendering is skipped when null |
| `UpdateRequested` | Per-frame CPU update callback |
| `RenderRequested` | Per-frame rendering callback with drawable texture |
| `UI(Action)` | Marshal work onto the framework UI thread |
| `EnsureResources()` | Create or recreate framework-specific presentation resources |
| `Tick()` | Raise update and render events for one frame |
| `Present()` | Present or blit the frame to the host UI surface |
| `ReleaseResources()` | Dispose framework-specific graphics resources |

Event args contracts:

- `UpdateEventArgs`: `DeltaSeconds`, `TotalSeconds`
- `RenderEventArgs`: `DeltaSeconds`, `TotalSeconds`, `Drawable` (`Texture`)

`RenderEventArgs` exposes a `Drawable` texture as the render target input.

## FrameScheduler Lifecycle

Each control owns a `FrameScheduler` that drives the loop:

1. UI lifecycle starts scheduler on load/create and stops it on unload/destroy.
2. Scheduler marshals frame work through `IZenithView.UI(...)`.
3. Frame body runs `EnsureResources()`, `Tick()`, then `Present()`.
4. When `GraphicsContext` changes, resources are released and recreated.

`UpdateSeconds`, `RenderSeconds`, and `TotalSeconds` are measured by internal stopwatches and propagated through event args.

## Framework Packages

| Package | UI Framework | Notes |
|---|---|---|
| `Zenith.NET.Views.WPF` | WPF | D3D11 shared texture path displayed through `D3DImage` |
| `Zenith.NET.Views.WinForms` | Windows Forms | Native Win32 swap chain (`Surface.Win32`) |
| `Zenith.NET.Views.WinUI` | WinUI 3 / Uno | Windows path uses `SwapChainPanel` + shared D3D11 texture; non-Windows Uno path uses CPU readback bitmap |
| `Zenith.NET.Views.Avalonia` | Avalonia | CPU readback (`Texture.Download`) into `WriteableBitmap` |
| `Zenith.NET.Views.Maui` | .NET MAUI | Handler-based platform view; swap chain on Android/iOS/MacCatalyst, shared D3D11 path on Windows |

Package references from project files:

- WPF: `Silk.NET.Direct3D11`, `Silk.NET.Direct3D9`
- WinUI (Windows target): `Microsoft.WindowsAppSDK`, `Silk.NET.Direct3D11`
- WinUI (non-Windows target): `Uno.WinUI`
- MAUI (Windows target): `Silk.NET.Direct3D11`
- Avalonia: `Avalonia`

## Surface and Present Paths

Current implementations use two conceptual presentation models.

Swap-chain-based:

- WinForms (`SwapChain` created from `Surface.Win32`)
- MAUI Android (`Surface.Android`)
- MAUI iOS/MacCatalyst (`Surface.Apple` / `CAMetalLayer`)

Shared-texture composition (Windows compositor interop):

- WPF (`D3DImage` + keyed mutex + D3D9/D3D11 bridge)
- WinUI Windows (`SwapChainPanel` + keyed mutex + copy into composition swap chain)
- MAUI Windows (`SwapChainPanel` + keyed mutex + copy into composition swap chain)

CPU readback presentation:

- Avalonia (`Texture.Download` into a locked bitmap buffer)
- WinUI Uno non-Windows (`Texture.Download` into `WriteableBitmap` pixel buffer)

## Swap-Chain Integration Pattern

Use one `GraphicsContext` and render into `args.Drawable` from `RenderRequested`. This example applies to the swap-chain-backed WinForms and MAUI Android/iOS/MacCatalyst controls:

```csharp
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Views;

GraphicsContext context = GraphicsContext.CreateDirectX12(useValidationLayer: true);
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

The required final layout depends on the control's presentation path:

- Swap-chain-backed controls require `TextureLayout.Present` before submission.
- Shared-texture and CPU-readback controls must not transition their drawable to `Present`; leave it in `ColorAttachment` and submit before the framework copies or downloads it.

The view's `FrameScheduler` calls `Present()` after `RenderRequested` returns. Waiting for the submitted commands in the callback ensures that shared-texture copies and CPU downloads observe completed rendering.

## Practical Guidance

- Keep event handlers lightweight; avoid allocations and pipeline creation inside per-frame callbacks.
- Recreate size-dependent resources only when view dimensions change.
- Treat `RenderRequested` as explicit rendering work: transitions, pass setup, and submission with the final layout required by that control.
- Respect framework lifetime events so `ReleaseResources()` runs before control teardown.

For Graphics API selection and package strategy, see [Graphics API Selection](backend-selection.md). For synchronization details, see [Synchronization and Barriers](../concepts/synchronization.md).
