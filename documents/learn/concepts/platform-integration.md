---
title: '@concepts.platforms.title'
---

<h1 id="platform-integration"><resource key="concepts.platforms.title"></resource></h1>
<p><resource key="concepts.platforms.description"></resource></p>
<p><a id="native-surfaces"></a></p>
<h2 id="supply-a-native-surface-when-the-application-owns-presentation"><resource key="concepts.platforms.surface.title"></resource></h2>
<p><resource key="concepts.platforms.surface.description"><slot name="link"><a href="~/learn/first-triangle.md#window"><resource key="concepts.platforms.surface.description.link"></resource></a></slot><slot name="surface"><a class="xref" href="xref:Zenith.NET.Surface"><code>Surface</code></a></slot><slot name="swapChain"><a class="xref" href="xref:Zenith.NET.SwapChain"><code>SwapChain</code></a></slot></resource></p>
<p><resource key="concepts.platforms.surface.details"></resource></p>
<table>
<thead>
<tr>
<th><resource key="concepts.platforms.surface.table.headings.factory"></resource></th>
<th><resource key="concepts.platforms.surface.table.headings.handles"></resource></th>
<th><resource key="concepts.platforms.surface.table.headings.backend"></resource></th>
</tr>
</thead>
<tbody>
<tr>
<td><a class="xref" href="xref:Zenith.NET.Surface.Win32(System.IntPtr,System.UInt32,System.UInt32)"><code>Win32</code></a></td>
<td><code>HWND</code></td>
<td><resource key="concepts.platforms.surface.table.win32.backend"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.Surface.Wayland(System.IntPtr,System.IntPtr,System.UInt32,System.UInt32)"><code>Wayland</code></a></td>
<td><resource key="concepts.platforms.surface.table.wayland.handles"></resource></td>
<td><resource key="concepts.platforms.surface.table.wayland.backend"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.Surface.Xlib(System.IntPtr,System.IntPtr,System.UInt32,System.UInt32)"><code>Xlib</code></a></td>
<td><resource key="concepts.platforms.surface.table.xlib.handles"></resource></td>
<td><resource key="concepts.platforms.surface.table.xlib.backend"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.Surface.Android(System.IntPtr,System.UInt32,System.UInt32)"><code>Android</code></a></td>
<td><code>ANativeWindow</code></td>
<td><resource key="concepts.platforms.surface.table.android.backend"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.Surface.Apple(System.IntPtr,System.UInt32,System.UInt32)"><code>Apple</code></a></td>
<td><code>CAMetalLayer</code></td>
<td><resource key="concepts.platforms.surface.table.apple.backend"></resource></td>
</tr>
</tbody>
</table>
<p><resource key="concepts.platforms.surface.guidance"><slot name="appCs"><a href="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/App.cs">App.cs</a></slot><slot name="surfaceWayland"><a class="xref" href="xref:Zenith.NET.Surface.Wayland(System.IntPtr,System.IntPtr,System.UInt32,System.UInt32)"><code>Surface.Wayland</code></a></slot></resource></p>
<p><resource key="concepts.platforms.surface.context"><slot name="contextCapabilities"><code>context.<a class="code-reference" href="xref:Zenith.NET.GraphicsContext.Capabilities">Capabilities</a></code></slot></resource></p>
<p><a id="resize"></a></p>
<h2 id="keep-the-drawables-size-and-lifetime-with-its-host"><resource key="concepts.platforms.lifecycle.title"></resource></h2>
<p><resource key="concepts.platforms.lifecycle.description"><slot name="swapChainDrawable"><a class="xref" href="xref:Zenith.NET.SwapChain.Drawable"><code>SwapChain.Drawable</code></a></slot></resource></p>
<p><resource key="concepts.platforms.lifecycle.details"><slot name="swapChainResize"><a class="xref" href="xref:Zenith.NET.SwapChain.Resize(System.UInt32,System.UInt32)"><code>SwapChain.Resize</code></a></slot><slot name="swapChainRefresh"><a class="xref" href="xref:Zenith.NET.SwapChain.Refresh(Zenith.NET.Surface)"><code>SwapChain.Refresh</code></a></slot></resource></p>
<p><resource key="concepts.platforms.lifecycle.guidance"><slot name="link"><a href="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs"><resource key="concepts.platforms.lifecycle.guidance.link"></resource></a></slot></resource></p>
<p><resource key="concepts.platforms.lifecycle.context"><slot name="descWidth"><code><a class="code-reference" href="xref:Zenith.NET.Texture.Desc">Desc</a>.<a class="code-reference" href="xref:Zenith.NET.TextureDesc.Width">Width</a></code></slot><slot name="descHeight"><code><a class="code-reference" href="xref:Zenith.NET.Texture.Desc">Desc</a>.<a class="code-reference" href="xref:Zenith.NET.TextureDesc.Height">Height</a></code></slot><slot name="descFormat"><code><a class="code-reference" href="xref:Zenith.NET.Texture.Desc">Desc</a>.<a class="code-reference" href="xref:Zenith.NET.TextureDesc.Format">Format</a></code></slot><slot name="descSampleCount"><code><a class="code-reference" href="xref:Zenith.NET.Texture.Desc">Desc</a>.<a class="code-reference" href="xref:Zenith.NET.TextureDesc.SampleCount">SampleCount</a></code></slot></resource></p>
<p><a id="ui-views"></a></p>
<h2 id="record-into-the-frame-supplied-by-a-ui-control"><resource key="concepts.platforms.frame.title"></resource></h2>
<p><resource key="concepts.platforms.frame.description"><slot name="iZenithView"><a class="xref" href="xref:Zenith.NET.Views.IZenithView"><code>IZenithView</code></a></slot><slot name="graphicsContext"><a class="xref" href="xref:Zenith.NET.Views.IZenithView.GraphicsContext"><code>GraphicsContext</code></a></slot><slot name="updateRequested"><a class="xref" href="xref:Zenith.NET.Views.IZenithView.UpdateRequested"><code>UpdateRequested</code></a></slot><slot name="renderRequested"><a class="xref" href="xref:Zenith.NET.Views.IZenithView.RenderRequested"><code>RenderRequested</code></a></slot><slot name="renderEventArgs"><a class="xref" href="xref:Zenith.NET.Views.RenderEventArgs"><code>RenderEventArgs</code></a></slot></resource></p>
<p><resource key="concepts.platforms.frame.details"><slot name="colorAttachment"><a class="xref" href="xref:Zenith.NET.TextureLayout.ColorAttachment"><code>ColorAttachment</code></a></slot></resource></p>
<p><resource key="concepts.platforms.frame.guidance"><slot name="iZenithView"><a class="xref" href="xref:Zenith.NET.Views.IZenithView"><code>IZenithView</code></a></slot><slot name="view"><code>view</code></slot><slot name="usingZenithNET"><code>using <a class="code-reference" href="xref:Zenith.NET">Zenith.NET</a>;</code></slot></resource></p>

```csharp
view.RenderRequested += (_, args) =>
{
    args.CommandBuffer.BeginRenderPass([ColorAttachment.Clear(args.Drawable, new(0.04f, 0.055f, 0.075f, 1.0f))], null);

    args.CommandBuffer.EndRenderPass();
};
```

<p><resource key="concepts.platforms.frame.context"><slot name="colorAttachment"><a class="xref" href="xref:Zenith.NET.TextureLayout.ColorAttachment"><code>ColorAttachment</code></a></slot></resource></p>
<p><resource key="concepts.platforms.frame.notes"><slot name="undefined"><a class="xref" href="xref:Zenith.NET.TextureLayout.Undefined"><code>Undefined</code></a></slot></resource></p>
<p><resource key="concepts.platforms.frame.reference"><slot name="zenithViewHelperDrawableFormat"><a class="xref" href="xref:Zenith.NET.Views.ZenithViewHelper.DrawableFormat"><code>ZenithViewHelper.DrawableFormat</code></a></slot></resource></p>
<h2 id="choose-the-presentation-path-as-well-as-the-framework"><resource key="concepts.platforms.integration.title"></resource></h2>
<p><resource key="concepts.platforms.integration.description"></resource></p>
<table>
<thead>
<tr>
<th><resource key="concepts.platforms.integration.table.headings.integration"></resource></th>
<th><resource key="concepts.platforms.integration.table.headings.presentation"></resource></th>
</tr>
</thead>
<tbody>
<tr>
<td><resource key="concepts.platforms.integration.table.avalonia.integration"></resource></td>
<td><resource key="concepts.platforms.integration.table.avalonia.presentation"><slot name="writeableBitmap"><code>WriteableBitmap</code></slot><slot name="zenithViewCs"><a href="https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.Avalonia/ZenithView.cs">ZenithView.cs</a></slot><slot name="surfaceCs"><a href="https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.Avalonia/Surface.cs">Surface.cs</a></slot></resource></td>
</tr>
<tr>
<td><resource key="concepts.platforms.integration.table.winForms.integration"></resource></td>
<td><resource key="concepts.platforms.integration.table.winForms.presentation"><slot name="hWND"><code>HWND</code></slot><slot name="zenithViewCs"><a href="https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.WinForms/ZenithView.cs">ZenithView.cs</a></slot></resource></td>
</tr>
<tr>
<td><resource key="concepts.platforms.integration.table.wpf.integration"></resource></td>
<td><resource key="concepts.platforms.integration.table.wpf.presentation"><slot name="d3DImage"><code>D3DImage</code></slot><slot name="surfaceCs"><a href="https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.WPF/Surface.cs">Surface.cs</a></slot></resource></td>
</tr>
<tr>
<td><resource key="concepts.platforms.integration.table.winUI.integration"></resource></td>
<td><resource key="concepts.platforms.integration.table.winUI.presentation"><slot name="swapChainPanel"><code>SwapChainPanel</code></slot><slot name="zenithViewWinUICs"><a href="https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.WinUI/ZenithView.WinUI.cs">ZenithView.WinUI.cs</a></slot></resource></td>
</tr>
<tr>
<td><resource key="concepts.platforms.integration.table.uno.integration"></resource></td>
<td><resource key="concepts.platforms.integration.table.uno.presentation"><slot name="writeableBitmap"><code>WriteableBitmap</code></slot><slot name="zenithViewUnoCs"><a href="https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.WinUI/ZenithView.Uno.cs">ZenithView.Uno.cs</a></slot></resource></td>
</tr>
<tr>
<td><resource key="concepts.platforms.integration.table.maui.integration"></resource></td>
<td><resource key="concepts.platforms.integration.table.maui.presentation"><slot name="link"><a href="https://github.com/qian-o/Zenith.NET/tree/master/sources/Views/Zenith.NET.Views.Maui/Platforms"><resource key="concepts.platforms.integration.table.maui.presentation.link"></resource></a></slot></resource></td>
</tr>
</tbody>
</table>
<p><resource key="concepts.platforms.integration.details"><slot name="renderRequested"><a class="xref" href="xref:Zenith.NET.Views.IZenithView.RenderRequested"><code>RenderRequested</code></a></slot></resource></p>
<p><resource key="concepts.platforms.integration.guidance"></resource></p>

```sh
dotnet add package Zenith.NET.Views.Avalonia
```

<p><resource key="concepts.platforms.integration.context"><slot name="useZenithView"><code>UseZenithView</code></slot><slot name="extensionsCs"><a href="https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views.Maui/Extensions.cs">Extensions.cs</a></slot></resource></p>
<h2 id="keep-application-resources-outside-the-controls-ownership"><resource key="concepts.platforms.ownership.title"></resource></h2>
<p><resource key="concepts.platforms.ownership.description"><slot name="graphicsContext"><a class="xref" href="xref:Zenith.NET.Views.IZenithView.GraphicsContext"><code>GraphicsContext</code></a></slot></resource></p>
<p><resource key="concepts.platforms.ownership.details"></resource></p>
<p><resource key="concepts.platforms.ownership.guidance"><slot name="frameSchedulerCs"><a href="https://github.com/qian-o/Zenith.NET/blob/master/sources/Views/Zenith.NET.Views/FrameScheduler.cs">FrameScheduler.cs</a></slot></resource></p>
