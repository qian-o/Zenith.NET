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
| **Vulkan 1.4** | Serves as the cross-platform fallback. While Vulkan has evolved rapidly with many extensions, mobile and Linux driver support remains uneven — so adaptation is driven by actual device capabilities rather than spec version alone |

For detailed backend selection guidance, see [Backend Selection](platform/backend-selection.md).

## Core Concepts

### Graphics Context

The `GraphicsContext` is the central hub of Zenith.NET. It abstracts the underlying graphics API and provides:

- **Resource Creation** - Create buffers, textures, pipelines, and other GPU resources
- **Command Queues** - Access to `Graphics`, `Compute`, and `Copy` queues
- **Capabilities** - Query device name and feature support via `Capabilities`

Backend-specific contexts are created via extension methods:
- `GraphicsContext.CreateDirectX12(useValidationLayer)` - Windows
- `GraphicsContext.CreateMetal(useValidationLayer)` - Apple
- `GraphicsContext.CreateVulkan(useValidationLayer)` - Cross-platform

### Command Model

Zenith.NET uses an explicit command recording model:

1. **Get a CommandBuffer** - Call `queue.CommandBuffer()` to obtain a buffer from the pool
2. **Record Commands** - Record draw calls, dispatches, copies, and state changes
3. **Submit** - Call `commandBuffer.Submit()` to execute on the GPU
4. **Synchronize** - Use `queue.WaitIdle()` to wait for all submitted work to complete

### Resource Binding

Resources are bound to shaders through two types:

| Type | Purpose |
|------|---------|
| `ResourceLayout` | Declares *what* resources a shader expects (binding slots, types) |
| `ResourceTable` | Provides *actual* resources matching a layout |

Pipelines reference a single `ResourceLayout`, and you bind a corresponding `ResourceTable` before draw/dispatch calls.

## Features

| Feature | Description |
|---------|-------------|
| **Graphics** | Traditional rasterization with vertex and pixel shaders |
| **Compute** | General-purpose GPU compute with compute shaders |
| **Ray Tracing** | Hardware-accelerated BLAS/TLAS with `RayQuery` in any shader stage |
| **Mesh Shading** | Modern GPU-driven geometry with mesh and amplification shaders |

## Platform Support

| Platform | DirectX 12 | Metal 4 | Vulkan 1.4 |
|----------|:----------:|:-----:|:------:|
| Windows  | <span class="status-yes">Yes</span> | <span class="status-no">No</span> | <span class="status-yes">Yes</span> |
| Linux    | <span class="status-no">No</span> | <span class="status-no">No</span> | <span class="status-yes">Yes</span> |
| Apple    | <span class="status-no">No</span> | <span class="status-yes">Yes</span> | <span class="status-yes">Yes</span> |
| Android  | <span class="status-no">No</span> | <span class="status-no">No</span> | <span class="status-yes">Yes</span> |

## Best Practices

### Resource Management

- **Dispose resources** when no longer needed using `using` statements or `IDisposable` patterns
- **Create resources upfront** rather than per-frame to avoid allocation overhead
- **Reuse command buffers** - the queue automatically pools and recycles them

### Command Recording

- **Batch similar operations** to reduce pipeline and resource table switches
- **Minimize render pass switches** by grouping draws with the same targets
- Call `queue.WaitIdle()` only when synchronization is required

### Data Alignment

Zenith.NET defines alignment constants in `GraphicsContext` for cross-platform compatibility:

| Constant | Value | Purpose |
|----------|:-----:|---------|
| `ConstantBufferAlignment` | 256 bytes | Minimum alignment for constant buffer data |
| `TextureRowPitchAlignment` | 256 bytes | Alignment for texture row pitch |
| `TextureDepthPitchAlignment` | 512 bytes | Alignment for 3D texture depth slice pitch |

## Next Steps

- [Tutorials](../tutorials/index.md) - Hands-on coding examples
- [API Reference](../api/index.md) - Detailed type documentation
