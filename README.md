<p align="center">
  <img src="documents/images/Zenith.NET.svg" alt="Zenith.NET icon" width="128" height="128">
</p>

<h1 align="center">Zenith.NET</h1>

<p align="center">
  A modern rendering hardware interface for .NET.
</p>

<p align="center">
  <a href="https://qian-o.github.io/Zenith.NET/learn/">Learn</a> ·
  <a href="https://qian-o.github.io/Zenith.NET/learn/samples.html">Samples</a> ·
  <a href="https://qian-o.github.io/Zenith.NET/api/">API Reference</a> ·
  <a href="https://www.nuget.org/packages?q=Zenith.NET">NuGet</a>
</p>

## Overview

Zenith.NET is a rendering hardware interface (RHI) that provides one C# API for graphics and compute across DirectX 12, Metal 4, and Vulkan 1.4. It gives applications explicit control over GPU resources, pipelines, command recording, synchronization, and presentation.

Build rasterization and compute workloads, access shader resources through bindless handles, and coordinate GPU work through queues, barriers, and timelines. Inline ray tracing and mesh shading are available through the same API when the device exposes the corresponding capabilities.

## Getting Started

In a .NET 10 project, install the core package and a backend supported by your platform and graphics driver. For Vulkan:

```console
dotnet add package Zenith.NET
dotnet add package Zenith.NET.Vulkan
```

Create a graphics context:

```csharp
using Zenith.NET;
using Zenith.NET.Vulkan;

using GraphicsContext context = GraphicsContext.CreateVulkan(useValidationLayer: true);

context.ValidationMessage += static (_, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");
```

Follow [First Triangle](https://qian-o.github.io/Zenith.NET/learn/first-triangle.html) to build a .NET console application with NuGet packages, from an empty window to the first rendered frame.

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

See [Platform Integration](https://qian-o.github.io/Zenith.NET/learn/concepts/platform-integration.html) for native surfaces, frame ownership and UI integration choices.

## Documentation and Examples

- [Learn](https://qian-o.github.io/Zenith.NET/learn/) — a complete triangle tutorial and guides to the programming model.
- [Samples](https://qian-o.github.io/Zenith.NET/learn/samples.html) — focused examples with C# and Slang source links.
- [API Reference](https://qian-o.github.io/Zenith.NET/api/) — types and members.
- [Experiments](sources/Experiments/) — sample applications and utilities in this repository.

## License

Zenith.NET is licensed under the [MIT License](LICENSE).
