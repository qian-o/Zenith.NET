<p align="center">
  <img src="documents/images/Zenith.NET-Logo.svg" alt="Zenith.NET" width="378">
</p>

<p align="center">
  A modern rendering hardware interface for .NET.
</p>

<p align="center">
  <a href="https://qian-o.github.io/Zenith.NET/docs/">Documentation</a> ·
  <a href="https://qian-o.github.io/Zenith.NET/tutorials/">Tutorials</a> ·
  <a href="https://qian-o.github.io/Zenith.NET/api/">API Reference</a> ·
  <a href="https://www.nuget.org/packages?q=Zenith.NET">NuGet</a>
</p>

## Overview

Zenith.NET provides one C# API for graphics and compute across DirectX 12, Metal 4, and Vulkan 1.4. It gives applications explicit control over GPU resources, pipelines, command recording, synchronization, and presentation.

Build rasterization and compute workloads, access shader resources through bindless handles, and coordinate GPU work through queues, barriers, and timelines. Inline ray tracing and mesh shading are available through the same API when the device exposes the corresponding capabilities.

## Getting Started

In a .NET 10 project, install the core package and the backend you want to use. For Vulkan:

```console
dotnet add package Zenith.NET
dotnet add package Zenith.NET.Vulkan
```

Create a graphics context:

```csharp
using Zenith.NET;
using Zenith.NET.Vulkan;

using GraphicsContext context = GraphicsContext.CreateVulkan(useValidationLayer: true);
```

Follow [Project Setup](https://qian-o.github.io/Zenith.NET/tutorials/getting-started/project-setup.html) to configure a complete application, then continue with [Hello Triangle](https://qian-o.github.io/Zenith.NET/tutorials/guides/hello-triangle.html) for the first rendering workload.

## Packages

Choose packages by their role in your application. Add a graphics backend, then the compiler, extensions, or UI integration you need.

### Core and Backends

| Package | Purpose |
| --- | --- |
| `Zenith.NET` | Shared graphics and compute API, resource types, and command interfaces. |
| `Zenith.NET.Compiler` | Compile Slang shaders for the selected graphics API. |
| `Zenith.NET.DirectX12` | DirectX 12 backend. |
| `Zenith.NET.Metal` | Metal 4 backend. |
| `Zenith.NET.Vulkan` | Vulkan 1.4 backend. |

### Extensions

| Package | Purpose |
| --- | --- |
| `Zenith.NET.Extensions.ImageSharp` | Load images into GPU textures with ImageSharp. |
| `Zenith.NET.Extensions.ImGui` | Dear ImGui rendering and input integration. |
| `Zenith.NET.Extensions.Skia` | SkiaSharp rendering with Zenith.NET textures. |
| `Zenith.NET.Extensions.Upscaling` | Spatial and temporal image upscaling. |

### UI Integrations

| Package | Purpose |
| --- | --- |
| `Zenith.NET.Views` | Shared rendering-view interfaces and frame events. |
| `Zenith.NET.Views.Avalonia` | Avalonia rendering control. |
| `Zenith.NET.Views.Maui` | .NET MAUI rendering control. |
| `Zenith.NET.Views.WinForms` | Windows Forms rendering control. |
| `Zenith.NET.Views.WinUI` | WinUI 3 and Uno rendering control. |
| `Zenith.NET.Views.WPF` | WPF rendering control. |

See [Views](https://qian-o.github.io/Zenith.NET/docs/presentation/views.html) for integrating rendering into a UI application.

## Documentation and Examples

- [RHI Guide](https://qian-o.github.io/Zenith.NET/docs/) — resources, shaders, pipelines, commands, synchronization, and presentation.
- [Tutorials](https://qian-o.github.io/Zenith.NET/tutorials/) — rendering and compute walkthroughs, from a triangle to ray tracing and mesh shading.
- [API Reference](https://qian-o.github.io/Zenith.NET/api/) — types and members.
- [Experiments](sources/Experiments/) — sample applications and utilities in this repository.

## License

Zenith.NET is licensed under the [MIT License](LICENSE).
