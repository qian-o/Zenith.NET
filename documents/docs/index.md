# Documentation

This section provides conceptual documentation to help you understand Zenith.NET's architecture and best practices.

> [!NOTE]
> Looking for step-by-step coding guides? Check out the [Tutorials](../tutorials/index.md) section.

## Core Concepts

### Graphics Context

The `GraphicsContext` is the central hub of Zenith.NET. It abstracts the underlying graphics API and provides:

- **Resource Creation** - Create buffers, textures, pipelines, and other GPU resources
- **Command Queues** - Access to `Graphics`, `Compute`, and `Copy` queues
- **Capabilities** - Query device name and feature support via `Capabilities`

Backend-specific contexts are created via extension methods:
- `GraphicsContext.CreateDirectX12(useValidationLayer)` - Windows
- `GraphicsContext.CreateMetal(useValidationLayer)` - macOS/iOS
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
| `ResourceSet` | Provides *actual* resources matching a layout |

Pipelines reference one or more `ResourceLayout` objects, and you bind corresponding `ResourceSet` objects before draw/dispatch calls.

## Pipeline Types

| Pipeline | Description |
|----------|-------------|
| `GraphicsPipeline` | Traditional rasterization with vertex and pixel shaders |
| `ComputePipeline` | General-purpose GPU compute with compute shaders |
| `RayTracingPipeline` | Hardware ray tracing with ray generation, hit, and miss shaders |
| `MeshShadingPipeline` | Modern GPU-driven geometry with mesh and amplification shaders |

## Platform Support

| Platform | DirectX 12 | Metal | Vulkan |
|----------|:----------:|:-----:|:------:|
| Windows  | <span class="status-yes">Yes</span> | <span class="status-no">No</span> | <span class="status-yes">Yes</span> |
| Linux    | <span class="status-no">No</span> | <span class="status-no">No</span> | <span class="status-yes">Yes</span> |
| macOS    | <span class="status-no">No</span> | <span class="status-yes">Yes</span> | <span class="status-yes">Yes</span> |
| iOS      | <span class="status-no">No</span> | <span class="status-yes">Yes</span> | <span class="status-yes">Yes</span> |
| Android  | <span class="status-no">No</span> | <span class="status-no">No</span> | <span class="status-yes">Yes</span> |

## Best Practices

### Resource Management

- **Dispose resources** when no longer needed using `using` statements or `IDisposable` patterns
- **Create resources upfront** rather than per-frame to avoid allocation overhead
- **Reuse command buffers** - the queue automatically pools and recycles them

### Command Recording

- **Batch similar operations** to reduce pipeline and resource set switches
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