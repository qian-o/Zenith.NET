<p align="center">
  <img src="https://raw.githubusercontent.com/qian-o/Zenith.NET/master/documents/images/Zenith.NET.png" alt="Zenith.NET icon" width="128" height="128">
</p>

<h1 align="center">Zenith.NET</h1>

<p align="center">
  Cross-platform graphics for .NET
</p>

<p align="center">
  <a href="https://qian-o.github.io/Zenith.NET/">Documentation</a> ·
  <a href="https://qian-o.github.io/Zenith.NET/learn/samples.html">Samples</a> ·
  <a href="https://qian-o.github.io/Zenith.NET/api/">API Reference</a> ·
  <a href="https://www.nuget.org/packages/Zenith.NET">NuGet</a>
</p>

Zenith.NET is a rendering hardware interface (RHI) for .NET, with a shared C# API for graphics and compute across **DirectX 12, Metal 4, and Vulkan 1.4**.

It supports Slang shaders and explicit GPU resource management, with ray tracing and mesh shading on supported devices. [Rendering controls](https://qian-o.github.io/Zenith.NET/learn/concepts/platform-integration.html#ui-views) are available for Avalonia, .NET MAUI, Uno, Windows Forms, WinUI, and WPF.

## Getting started

Follow [First Triangle](https://qian-o.github.io/Zenith.NET/learn/first-triangle.html) to create a .NET 10 console application and draw your first triangle.

## Packages

### Core and backends

| Package | Purpose |
| --- | --- |
| [Zenith.NET](https://www.nuget.org/packages/Zenith.NET) | Shared graphics and compute API. |
| [Zenith.NET.Compiler](https://www.nuget.org/packages/Zenith.NET.Compiler) | Slang shader compilation for each graphics backend. |
| [Zenith.NET.DirectX12](https://www.nuget.org/packages/Zenith.NET.DirectX12) | DirectX 12 backend. |
| [Zenith.NET.Metal](https://www.nuget.org/packages/Zenith.NET.Metal) | Metal 4 backend. |
| [Zenith.NET.Vulkan](https://www.nuget.org/packages/Zenith.NET.Vulkan) | Vulkan 1.4 backend. |

### Extensions

| Package | Purpose |
| --- | --- |
| [Zenith.NET.Extensions.ImageSharp](https://www.nuget.org/packages/Zenith.NET.Extensions.ImageSharp) | Image loading and texture creation with ImageSharp. |
| [Zenith.NET.Extensions.ImGui](https://www.nuget.org/packages/Zenith.NET.Extensions.ImGui) | Dear ImGui rendering and input integration. |
| [Zenith.NET.Extensions.Skia](https://www.nuget.org/packages/Zenith.NET.Extensions.Skia) | SkiaSharp drawing on GPU textures. |
| [Zenith.NET.Extensions.Upscaling](https://www.nuget.org/packages/Zenith.NET.Extensions.Upscaling) | Spatial and temporal image upscaling. |

### UI integrations

| Package | Purpose |
| --- | --- |
| [Zenith.NET.Views](https://www.nuget.org/packages/Zenith.NET.Views) | Shared interfaces and frame events for rendering controls. |
| [Zenith.NET.Views.Avalonia](https://www.nuget.org/packages/Zenith.NET.Views.Avalonia) | Avalonia rendering control. |
| [Zenith.NET.Views.Maui](https://www.nuget.org/packages/Zenith.NET.Views.Maui) | .NET MAUI rendering control. |
| [Zenith.NET.Views.WinForms](https://www.nuget.org/packages/Zenith.NET.Views.WinForms) | Windows Forms rendering control. |
| [Zenith.NET.Views.WinUI](https://www.nuget.org/packages/Zenith.NET.Views.WinUI) | WinUI and Uno Platform rendering control. |
| [Zenith.NET.Views.WPF](https://www.nuget.org/packages/Zenith.NET.Views.WPF) | WPF rendering control. |

## License

[MIT](https://github.com/qian-o/Zenith.NET/blob/master/LICENSE)
