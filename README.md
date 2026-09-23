<p align="center">
  <img src="https://raw.githubusercontent.com/qian-o/Zenith.NET/master/documents/images/Zenith.NET.png" alt="Zenith.NET icon" width="128" height="128">
</p>

<h1 align="center">Zenith.NET</h1>

<p align="center">
  Cross-platform graphics for .NET
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Zenith.NET"><img src="https://img.shields.io/nuget/v/Zenith.NET?label=stable" alt="NuGet stable version"></a>
  <a href="https://www.nuget.org/packages/Zenith.NET?prerelease=true"><img src="https://img.shields.io/nuget/vpre/Zenith.NET?label=prerelease" alt="NuGet prerelease version"></a>
  <a href="https://github.com/qian-o/Zenith.NET/actions/workflows/continuous-integration.yml"><img src="https://github.com/qian-o/Zenith.NET/actions/workflows/continuous-integration.yml/badge.svg?branch=master" alt="Continuous Integration status"></a>
</p>

<p align="center">
  <a href="https://qian-o.github.io/Zenith.NET/">Documentation</a> ·
  <a href="https://qian-o.github.io/Zenith.NET/learn/samples.html">Samples</a> ·
  <a href="https://qian-o.github.io/Zenith.NET/api/">API Reference</a> ·
  <a href="https://www.nuget.org/packages/Zenith.NET">NuGet</a>
</p>

Zenith.NET is a rendering hardware interface (RHI) for .NET, with a shared C# API for graphics and compute across **DirectX 12, Metal 4, and Vulkan 1.4**.

## Features

- Create buffers, textures, views, heaps, and pipelines for graphics and compute through the shared API.
- Record graphics, compute, and transfer work in command buffers. Use barriers and texture transitions to order dependent accesses, and timelines to coordinate queue submissions.
- Compile Slang shaders for the selected graphics API with `Zenith.NET.Compiler`.
- Use ray queries and mesh shading when the device reports support.
- Present through native swap chains or optional UI rendering controls.

## Getting started

Follow [First Triangle](https://qian-o.github.io/Zenith.NET/learn/first-triangle.html) to create a .NET 10 console application and draw your first triangle.

Continue with [Learn](https://qian-o.github.io/Zenith.NET/learn/index.html) for the core concepts, or browse [Samples](https://qian-o.github.io/Zenith.NET/learn/samples.html) for graphics and compute examples.

## Packages

### Graphics packages

| Package | Purpose |
| --- | --- |
| [Zenith.NET](https://www.nuget.org/packages/Zenith.NET) | Shared graphics and compute API. |
| [Zenith.NET.Compiler](https://www.nuget.org/packages/Zenith.NET.Compiler) | Compiles Slang shaders for the selected graphics API. |
| [Zenith.NET.DirectX12](https://www.nuget.org/packages/Zenith.NET.DirectX12) | DirectX 12 implementation of the shared API. |
| [Zenith.NET.Metal](https://www.nuget.org/packages/Zenith.NET.Metal) | Metal 4 implementation of the shared API. |
| [Zenith.NET.Vulkan](https://www.nuget.org/packages/Zenith.NET.Vulkan) | Vulkan 1.4 implementation of the shared API. |

### Extensions

| Package | Purpose |
| --- | --- |
| [Zenith.NET.Extensions.ImageSharp](https://www.nuget.org/packages/Zenith.NET.Extensions.ImageSharp) | Loads images into GPU textures using ImageSharp. |
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

## Questions

Ask usage questions in [GitHub Discussions](https://github.com/qian-o/Zenith.NET/discussions).

## License

[MIT](https://github.com/qian-o/Zenith.NET/blob/master/LICENSE)
