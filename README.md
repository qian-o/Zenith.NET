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

## Graphics Backends

Zenith.NET supports multiple graphics APIs that cover mainstream rendering technologies, making it easy to choose the right backend for your scenario.

| API       | Version | Supported |
| :-------: | :-----: | :-------: |
| DirectX12 | 12.0    | planned   |
| Metal     | 3.0     | planned   |
| Vulkan    | 1.3     | completed |

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