<p align="center">
  <img src="documents/images/Zenith.NET.svg" alt="Zenith.NET Logo" width="128" height="128">
</p>

<h1 align="center">Zenith.NET</h1>

<p align="center">
  A modern rendering hardware interface for .NET.<br/>
  Build graphics and compute workloads with one consistent C# API.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Zenith.NET"><img src="https://img.shields.io/nuget/v/Zenith.NET.svg?style=flat-square" alt="NuGet"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/qian-o/Zenith.NET?style=flat-square" alt="License"></a>
</p>

---

## Overview

Zenith.NET provides a compact C# API for resources, pipelines, command recording, synchronization, and presentation. Applications can use the same rendering model with DirectX 12, Metal 4, or Vulkan 1.4.

The RHI supports rasterization, compute, indirect commands, inline Ray Tracing, and mesh shading. Bindless resource handles keep shader binding straightforward, while explicit queues and resource states make execution order clear.

## Get Started

Install the core package and one graphics API package:

```powershell
dotnet add package Zenith.NET
dotnet add package Zenith.NET.Vulkan
```

Create a graphics context in C#:

```csharp
using Zenith.NET;
using Zenith.NET.Vulkan;

using GraphicsContext context = GraphicsContext.CreateVulkan(useValidationLayer: true);
```

Continue with the [RHI Guide](https://qian-o.github.io/Zenith.NET/docs/) or build the examples in the [Tutorials](https://qian-o.github.io/Zenith.NET/tutorials/).

## Platform Support

|           | DirectX 12 | Metal 4 | Vulkan 1.4 |
| :-------: | :--------: | :-----: | :--------: |
| Windows   | Yes |  | Yes |
| Apple     |  | Yes | Yes |
| Android   |  |  | Yes |
| Linux     |  |  | Yes |

## Packages

### Core

[![Zenith.NET](https://img.shields.io/nuget/v/Zenith.NET.svg?label=Zenith.NET&style=flat-square)](https://www.nuget.org/packages/Zenith.NET)

### Backends

[![DirectX12](https://img.shields.io/nuget/v/Zenith.NET.DirectX12.svg?label=DirectX12&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.DirectX12)
[![Metal](https://img.shields.io/nuget/v/Zenith.NET.Metal.svg?label=Metal&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Metal)
[![Vulkan](https://img.shields.io/nuget/v/Zenith.NET.Vulkan.svg?label=Vulkan&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Vulkan)

### Extensions

[![ImageSharp](https://img.shields.io/nuget/v/Zenith.NET.Extensions.ImageSharp.svg?label=ImageSharp&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Extensions.ImageSharp)
[![ImGui](https://img.shields.io/nuget/v/Zenith.NET.Extensions.ImGui.svg?label=ImGui&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Extensions.ImGui)
[![Skia](https://img.shields.io/nuget/v/Zenith.NET.Extensions.Skia.svg?label=Skia&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Extensions.Skia)

### Views

[![Views](https://img.shields.io/nuget/v/Zenith.NET.Views.svg?label=Views&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views)
[![Avalonia](https://img.shields.io/nuget/v/Zenith.NET.Views.Avalonia.svg?label=Avalonia&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views.Avalonia)
[![MAUI](https://img.shields.io/nuget/v/Zenith.NET.Views.Maui.svg?label=MAUI&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views.Maui)
[![WinForms](https://img.shields.io/nuget/v/Zenith.NET.Views.WinForms.svg?label=WinForms&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views.WinForms)
[![WinUI](https://img.shields.io/nuget/v/Zenith.NET.Views.WinUI.svg?label=WinUI&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views.WinUI)
[![WPF](https://img.shields.io/nuget/v/Zenith.NET.Views.WPF.svg?label=WPF&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views.WPF)

## Documentation

- [RHI Guide](https://qian-o.github.io/Zenith.NET/docs/)
- [Tutorials](https://qian-o.github.io/Zenith.NET/tutorials/)
- [API Reference](https://qian-o.github.io/Zenith.NET/api/)
