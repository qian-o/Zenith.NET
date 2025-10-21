# Zenith.NET

Modern, cross-platform graphics and compute library for .NET. It provides a unified GPU programming interface to simplify graphics rendering and general-purpose compute workflows.

> Status: Work in progress (pre-release)

## Overview

Zenith.NET targets modern .NET (including .NET 9) and integrates with multiple UI frameworks (including .NET MAUI) to enable portable, high-performance rendering across platforms.

### Highlights
- Unified, backend-agnostic GPU API surface
- Multiple graphics backends (DirectX12, Metal, Vulkan)
- View integrations across popular .NET UI frameworks
- Designed for performance and ease of integration
- Cross-platform support, consistent developer experience

## Graphics Backends

Zenith.NET supports multiple graphics APIs covering mainstream rendering technologies, making it easy to choose the right backend based on your needs.

| API       | Supported |
| :-------: | :-------: |
| DirectX12 | planned   |
| Metal     | planned   |
| Vulkan    | planned   |

## View Backends

Zenith.NET supports multiple .NET UI frameworks, and its rendering capabilities can be seamlessly integrated into different types of applications.

| Framework   | CPU Pixel Copy | Native GPU Rendering | DirectX12 | Metal     | Vulkan    |
| :---------: | :------------: | :------------------: | :-------: | :-------: | :-------: |
| Avalonia    | supported      |                      | supported | supported | supported |
| MAUI        |                | supported            | supported | supported | supported |
| WinForms    |                | supported            | supported |           | supported |
| WinUI       |                | supported            | supported |           | supported |
| WinUI (Uno) | supported      |                      | supported | supported | supported |
| WPF         |                | supported            | supported |           | supported |

Note: The CPU Pixel Copy and Native GPU Rendering columns indicate internal capabilities only. Selection is automatic and depends on the framework/platform integration.
