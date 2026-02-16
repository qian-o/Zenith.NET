# API Reference

Welcome to the Zenith.NET API Reference. This documentation is automatically generated from the source code and provides detailed information about all public types, methods, and properties.

## Namespaces

| Namespace | Description |
|-----------|-------------|
| Zenith.NET | Core graphics abstractions and resource types |
| Zenith.NET.DirectX12 | DirectX 12 backend implementation for Windows |
| Zenith.NET.Metal | Metal backend implementation for Apple platforms |
| Zenith.NET.Vulkan | Vulkan backend implementation for cross-platform support |

## Key Types

### Context & Queues

| Type | Description |
|------|-------------|
| `GraphicsContext` | Central hub for creating GPU resources and accessing command queues |
| `CommandQueue` | Provides command buffers and synchronization (Graphics, Compute, Copy) |
| `CommandBuffer` | Records and submits GPU commands |
| `Capabilities` | Reports device name and feature support (ray tracing, mesh shading) |

### Presentation

| Type | Description |
|------|-------------|
| `SwapChain` | Manages double/triple buffering and presentation to a surface |
| `FrameBuffer` | Render target attachments (color and depth/stencil) |

### Buffers & Textures

| Type | Description |
|------|-------------|
| `Buffer` | GPU buffer for vertex, index, constant, or structured data |
| `BufferView` | A view into a portion of a buffer for shader binding |
| `Texture` | GPU texture resource (2D, 3D, Cube, Array) |
| `TextureView` | A view into a texture for shader binding |
| `Sampler` | Texture sampling and filtering configuration |

### Pipelines

| Type | Description |
|------|-------------|
| `GraphicsPipeline` | Rasterization pipeline configuration |
| `ComputePipeline` | Compute dispatch pipeline configuration |
| `MeshShadingPipeline` | Mesh shading pipeline configuration |

### Resource Binding

| Type | Description |
|------|-------------|
| `ResourceLayout` | Declares expected shader resource bindings |
| `ResourceTable` | Binds actual resources to a layout |
| `Shader` | Compiled shader module with entry point and stage |

### Ray Tracing

| Type | Description |
|------|-------------|
| `BottomLevelAccelerationStructure` | BLAS containing triangle or AABB geometry |
| `TopLevelAccelerationStructure` | TLAS containing geometry instances |

### Query

| Type | Description |
|------|-------------|
| `QueryHeap` | GPU query heap for timestamps and statistics |

## Extensions

| Namespace | Description |
|-----------|-------------|
| Zenith.NET.Extensions.ImageSharp | Texture loading from files/streams via ImageSharp |
| Zenith.NET.Extensions.ImGui | Dear ImGui rendering integration |
| Zenith.NET.Extensions.Skia | Skia rendering integration |
| Zenith.NET.Extensions.Slang | Shader compilation from Slang source files |

## Views

| Namespace | Description |
|-----------|-------------|
| Zenith.NET.Views | Base view interface and frame scheduling |
| Zenith.NET.Views.Avalonia | Avalonia UI integration |
| Zenith.NET.Views.Maui | .NET MAUI integration |
| Zenith.NET.Views.WinForms | Windows Forms integration |
| Zenith.NET.Views.WinUI | WinUI 3 and Uno Platform integration |
| Zenith.NET.Views.WPF | WPF integration |

## Navigation

Browse the namespace tree on the left to explore all available types. Each type page includes:

- **Summary** - Brief description of the type
- **Properties** - Available properties
- **Methods** - Available methods

> [!TIP]
> Start with `GraphicsContext` to understand resource creation, then explore `CommandBuffer` for recording GPU commands.
