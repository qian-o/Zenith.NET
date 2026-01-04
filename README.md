# Zenith.NET

A modern, cross-platform graphics and compute library for .NET. It provides a unified GPU programming interface to simplify rendering and general-purpose compute workflows.

> Status: Work in progress (pre-release)

## Overview

Zenith.NET targets modern .NET (including .NET 10.0) and integrates with multiple UI frameworks (such as .NET MAUI) to enable portable, high-performance rendering across platforms.

### Highlights
- Unified, backend-agnostic GPU API
- Multiple graphics backends (DirectX12, Metal, Vulkan)
- First-class integrations with popular .NET UI frameworks
- Designed for performance and easy integration
- Consistent, cross-platform developer experience

## NuGet Packages

### Core

| Package    | Description                       | NuGet                                                                                                                  |
| :--------- | :-------------------------------- | :--------------------------------------------------------------------------------------------------------------------: |
| Zenith.NET | Core graphics abstraction library | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.svg)](https://www.nuget.org/packages/Zenith.NET)                   |

### Backends

| Package              | Description                      | NuGet                                                                                                                                  |
| :------------------- | :------------------------------- | :------------------------------------------------------------------------------------------------------------------------------------: |
| Zenith.NET.DirectX12 | DirectX12 backend implementation | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.DirectX12.svg)](https://www.nuget.org/packages/Zenith.NET.DirectX12)               |
| Zenith.NET.Metal     | Metal backend implementation     | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Metal.svg)](https://www.nuget.org/packages/Zenith.NET.Metal)                       |
| Zenith.NET.Vulkan    | Vulkan backend implementation    | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Vulkan.svg)](https://www.nuget.org/packages/Zenith.NET.Vulkan)                     |

### Extensions

| Package                          | Description                          | NuGet                                                                                                                                                      |
| :------------------------------- | :----------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------------------: |
| Zenith.NET.Extensions.ImageSharp | ImageSharp texture loading extension | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Extensions.ImageSharp.svg)](https://www.nuget.org/packages/Zenith.NET.Extensions.ImageSharp)           |
| Zenith.NET.Extensions.ImGui      | Dear ImGui integration               | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Extensions.ImGui.svg)](https://www.nuget.org/packages/Zenith.NET.Extensions.ImGui)                     |
| Zenith.NET.Extensions.Skia       | SkiaSharp integration                | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Extensions.Skia.svg)](https://www.nuget.org/packages/Zenith.NET.Extensions.Skia)                       |
| Zenith.NET.Extensions.Slang      | Slang shader compiler extension      | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Extensions.Slang.svg)](https://www.nuget.org/packages/Zenith.NET.Extensions.Slang)                     |

### Views

| Package                   | Description                        | NuGet                                                                                                                                          |
| :------------------------ | :--------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------: |
| Zenith.NET.Views          | Shared UI view abstractions        | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Views.svg)](https://www.nuget.org/packages/Zenith.NET.Views)                               |
| Zenith.NET.Views.Avalonia | Avalonia UI integration            | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Views.Avalonia.svg)](https://www.nuget.org/packages/Zenith.NET.Views.Avalonia)             |
| Zenith.NET.Views.Maui     | .NET MAUI integration              | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Views.Maui.svg)](https://www.nuget.org/packages/Zenith.NET.Views.Maui)                     |
| Zenith.NET.Views.WinForms | Windows Forms integration          | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Views.WinForms.svg)](https://www.nuget.org/packages/Zenith.NET.Views.WinForms)             |
| Zenith.NET.Views.WinUI    | WinUI 3 / Uno Platform integration | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Views.WinUI.svg)](https://www.nuget.org/packages/Zenith.NET.Views.WinUI)                   |
| Zenith.NET.Views.WPF      | WPF integration                    | [![NuGet](https://img.shields.io/nuget/v/Zenith.NET.Views.WPF.svg)](https://www.nuget.org/packages/Zenith.NET.Views.WPF)                       |

## Graphics Backends

Zenith.NET supports multiple graphics APIs that cover mainstream rendering technologies, making it easy to choose the right backend for your scenario.

| API       | Feature Level / API Version | Supported |
| :-------: | :-------------------------: | :-------: |
| DirectX12 | 12_0                        | completed |
| Metal     | 4.0                         | planned   |
| Vulkan    | 1.4                         | completed |

## UI Framework Integrations

Zenith.NET supports multiple .NET UI frameworks, and its rendering capabilities can be seamlessly integrated into different types of applications.

| Framework   | CPU Pixel Copy | Native GPU Rendering | DirectX12 | Metal     | Vulkan    |
| :---------: | :------------: | :------------------: | :-------: | :-------: | :-------: |
| Avalonia    | supported      |                      | supported | supported | supported |
| MAUI        |                | supported            | supported | supported | supported |
| WinForms    |                | supported            | supported |           | supported |
| WinUI       |                | supported            | supported |           | supported |
| WinUI (Uno) | supported      |                      | supported | supported | supported |
| WPF         |                | supported            | supported |           | supported |

Note: "CPU Pixel Copy" and "Native GPU Rendering" are two ways to present frames in a UI view. The former copies pixel data on the CPU; the latter renders directly to the view using the GPU.