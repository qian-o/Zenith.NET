# Documentation

This section provides conceptual documentation to help you understand Zenith.NET's architecture and best practices.

> [!NOTE]
> Looking for step-by-step coding guides? Check out the [Tutorials](../tutorials/index.md) section.

## Design Philosophy

Zenith.NET abstracts DirectX 12, Metal 4, and Vulkan 1.4 under a unified API. The design follows a clear principle: **adopt the latest API versions and expose only the capabilities shared across all three backends**. This means platform-specific features are intentionally excluded to maintain a consistent cross-platform experience.

Each backend targets real-world device coverage:

| Backend | Strategy |
|---------|----------|
| **DirectX 12** | Targets mainstream Windows 10 and above, covering the vast majority of Windows devices |
| **Metal 4** | Supports Apple Silicon (M-series) Macs and compatible iPhone/iPad models. Intel-based Macs are not supported |
| **Vulkan 1.4** | Cross-platform fallback, requiring Vulkan 1.4 as the minimum version |

## Core Concepts

| Topic | Description |
|-------|-------------|
| [Graphics Context](concepts/graphics-context.md) | The central hub for resource creation, command queues, capabilities, and validation |
| [Command Model](concepts/command-model.md) | Command buffers, submission, synchronization, and the full command API |
| [Resource Binding](concepts/resource-binding.md) | `ResourceTable`, `ResourceBinding`, and how resources connect to shaders |

## Resources

| Topic | Description |
|-------|-------------|
| [Buffers](resources/buffers.md) | Vertex, index, constant, and structured buffers with upload and map operations |
| [Textures](resources/textures.md) | 2D/3D/Cube textures, pixel formats, mipmaps, and multisampling |
| [Samplers](resources/samplers.md) | Address modes, filtering, anisotropic sampling, and LOD control |

## Features

| Feature | Description |
|---------|-------------|
| [Graphics](features/graphics.md) | Rasterization with vertex/pixel shaders, render states, and input layouts |
| [Compute](features/compute.md) | General-purpose GPU computing with dispatch and read-write resources |
| [Ray Tracing](features/ray-tracing.md) | BLAS/TLAS acceleration structures and `RayQuery` inline tracing |
| [Mesh Shading](features/mesh-shading.md) | GPU-driven geometry with mesh and amplification shaders |

## Platform

| Topic | Description |
|-------|-------------|
| [Backend Selection](platform/backend-selection.md) | Platform-backend mapping, runtime detection, and capability checks |
| [UI Framework Integration](platform/ui-frameworks.md) | Avalonia, MAUI, WinForms, WinUI, and WPF view controls |

## [Best Practices](best-practices.md)

Resource lifecycle, command batching, data alignment, performance tips, and debugging guidance.

## Next Steps

- [Tutorials](../tutorials/index.md) - Hands-on coding examples
- [API Reference](../api/index.md) - Detailed type documentation
