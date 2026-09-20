# Platform Integration

A renderer can present directly into a native window or draw through a UI framework's control. The host is the application code or UI framework responsible for displaying frames. It determines who manages the drawable—the texture rendered into for the current frame—as well as submission, presentation and resize. Offscreen graphics and compute work do not require a window or swap chain.

<a id="native-surfaces"></a>
## Supply a native surface when the application owns presentation

The [triangle](../first-triangle.md#window) uses Silk.NET to create a window and process events. Zenith.NET connects to that window through a [`Surface`](xref:Zenith.NET.Surface) description and a [`SwapChain`](xref:Zenith.NET.SwapChain). A surface contains native handles and dimensions; constructing the description does not create or own the operating-system window.

The handle must match the selected backend:

| Surface factory | Handles supplied by the host | Backend path |
| --- | --- | --- |
| `Win32` | `HWND` | DirectX 12 or Vulkan on Windows. |
| `Wayland` | Wayland display and surface | Vulkan with a native Wayland host. |
| `Xlib` | X11 display and window | Vulkan on Linux with X11 or XWayland. |
| `Android` | `ANativeWindow` | Vulkan on Android. |
| `Apple` | `CAMetalLayer` | Metal on Apple platforms. |

The tutorial's [App.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/App.cs) shows the Windows, macOS and X11 choices. Its Linux host supplies Xlib handles; using it in a Wayland session requires XWayland. A native Wayland application would supply different handles through `Surface.Wayland`.

A windowing library or UI framework supporting an operating system does not guarantee that the selected GPU backend is available there. Device features, drivers and native surface support still have to match. Check optional ray tracing and mesh shading through `context.Capabilities` before creating those workloads.

<a id="resize"></a>
## Keep the drawable's size and lifetime with its host

For a native swap chain, pass framebuffer dimensions in pixels. Window sizes expressed in logical units can differ when display scaling is enabled. Skip zero-size framebuffers, resize the swap chain when its pixel size changes, and obtain `SwapChain.Drawable` for each frame. The drawable is owned by the swap chain and can change after presentation or resize.

`SwapChain.Resize` changes the dimensions of the existing surface. `SwapChain.Refresh` replaces the surface description, including its native handles. Before either operation, complete outstanding uses of the affected resources. Dispose the swap chain before destroying its native presentation target.

Recreate application-owned depth or offscreen textures whose size follows the window, and update constants containing their handles. A pipeline can remain in use across a size change when its attachment formats and sample count remain compatible. The [spinning cube renderer](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs) keeps its pipeline while replacing its depth texture.

For a UI view, use the event drawable's `Desc.Width`, `Desc.Height`, `Desc.Format` and `Desc.SampleCount` when creating matching rendering resources. The control determines that drawable's resolution; do not assume its logical dimensions or display scale always equal the texture dimensions.

<a id="ui-views"></a>
## Record into the frame supplied by a UI control

The integrations implement [`IZenithView`](xref:Zenith.NET.Views.IZenithView). The application supplies its `GraphicsContext`, updates scene data in `UpdateRequested`, and records rendering in `RenderRequested` using [`RenderEventArgs`](xref:Zenith.NET.Views.RenderEventArgs).

The control owns the frame boundary. It obtains the drawable and command buffer, transitions the drawable to `ColorAttachment`, invokes the handlers, then performs the submission and presentation steps required by that framework.

For an initialized `IZenithView` named `view`, with a context assigned, this handler records a complete clear pass. It requires `using Zenith.NET;`:

```csharp
view.RenderRequested += (_, args) =>
{
    args.CommandBuffer.BeginRenderPass([ColorAttachment.Clear(args.Drawable, new(0.04f, 0.055f, 0.075f, 1.0f))], null);

    args.CommandBuffer.EndRenderPass();
};
```

Add your pipeline bindings and draw commands inside that pass. End the pass before returning. Do not submit the supplied command buffer or present independently, and do not dispose the command buffer or drawable. If your handler uses the drawable in another layout, return it to `ColorAttachment` for the control's final transition.

The controls transition from `Undefined` at the start of each frame, so applications cannot rely on the drawable's previous contents being preserved. Clear or render the required image each frame. Keep persistent accumulation or cached rendering in an application-owned texture, then display that texture through the view.

[`ZenithViewHelper.DrawableFormat`](xref:Zenith.NET.Views.ZenithViewHelper.DrawableFormat) supplies the integrations' default color format. Check the actual event drawable when matching attachment formats, sample counts and dimensions.

## Choose the presentation path as well as the framework

The controls share the event interface, but they do not all present in the same way:

| Integration | How its image reaches the UI |
| --- | --- |
| Avalonia | Renders to a texture and downloads pixels into a `WriteableBitmap`. See [ZenithView.cs](https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.Avalonia/ZenithView.cs) and [Surface.cs](https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.Avalonia/Surface.cs). |
| WinForms | Creates a Zenith.NET swap chain for the control's `HWND`. See [ZenithView.cs](https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.WinForms/ZenithView.cs). |
| WPF | Imports a shared Direct3D 11 texture, then copies through Direct3D interop to a surface displayed by `D3DImage`. See [Surface.cs](https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.WPF/Surface.cs). |
| WinUI on Windows | Renders into an imported shared Direct3D 11 texture, then copies to a composition swap chain attached to `SwapChainPanel`. See [ZenithView.WinUI.cs](https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.WinUI/ZenithView.WinUI.cs). |
| Uno path | Downloads pixels into a `WriteableBitmap`. See [ZenithView.Uno.cs](https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.WinUI/ZenithView.Uno.cs). |
| .NET MAUI | Uses native surfaces on Android, iOS and Mac Catalyst, and shared-texture composition on Windows. See the [platform implementations](https://github.com/qian-o/Zenith.NET/tree/master/sources/Views/Zenith.NET.Views.Maui/Platforms). |

Bitmap readback copies rendered pixels from GPU storage into CPU memory before the UI displays them. Shared-texture interop avoids that pixel readback path but can still perform GPU copies and synchronization. Do not assume a shared `RenderRequested` interface implies the same presentation cost or native interop requirements.

Use the package for the application's framework; for example:

```sh
dotnet add package Zenith.NET.Views.Avalonia
```

WinForms, WPF and the Windows WinUI path require Windows. MAUI applications need their target platform's workload and must register the control handler with `UseZenithView`; see [Extensions.cs](https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.Maui/Extensions.cs). Framework support and backend support must both be satisfied.

## Keep application resources outside the control's ownership

The application owns the context it assigns and the resources it creates for rendering. The control owns its own presentation resources. Changing `GraphicsContext` makes the scheduler recreate the control's resources; it does not recreate the application's pipelines, buffers or textures for the new context.

Coordinate resource replacement with the view's frame lifecycle. Stop the view's frame activity and finish outstanding GPU uses before disposing application resources or the assigned context. Removing a view does not transfer context ownership to the control.

The shared [FrameScheduler.cs](https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views/FrameScheduler.cs) dispatches frame work through the view's UI dispatcher. Keep frame handlers focused on updating and recording the frame; lengthy CPU work there also delays the UI.
