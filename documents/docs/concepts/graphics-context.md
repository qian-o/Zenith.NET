# Graphics Context

The `GraphicsContext` is the central hub of Zenith.NET. All GPU resources are created through it, and it provides access to the three command queues used for submitting work to the GPU.

All GPU resources (buffers, textures, pipelines, resource tables, acceleration structures, etc.) implement `IDisposable` and must be explicitly disposed when no longer needed. Dispose all resources before disposing the `GraphicsContext` itself.

## Creating a Context

Backend-specific contexts are created via static extension methods:

```csharp
// Windows — DirectX 12
GraphicsContext context = GraphicsContext.CreateDirectX12(useValidationLayer: true);

// Apple — Metal 4
GraphicsContext context = GraphicsContext.CreateMetal(useValidationLayer: true);

// Cross-platform — Vulkan 1.4
GraphicsContext context = GraphicsContext.CreateVulkan(useValidationLayer: true);
```

The `useValidationLayer` parameter enables runtime validation — invaluable during development, but should be disabled in production for performance.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Backend` | `Backend` | The active backend (`DirectX12`, `Metal`, or `Vulkan`) |
| `Capabilities` | `Capabilities` | Device name and feature support queries |
| `Graphics` | `CommandQueue` | Queue for draw, dispatch, and copy commands |
| `Compute` | `CommandQueue` | Queue for compute dispatches and copies |
| `Copy` | `CommandQueue` | Queue for data transfer only |

## Resource Creation

All GPU resources are created through `GraphicsContext`:

| Method | Returns | Description |
|--------|---------|-------------|
| `CreateSwapChain` | `SwapChain` | Presentation chain for a window surface |
| `CreateFrameBuffer` | `FrameBuffer` | Off-screen render target attachments |
| `CreateShader` | `Shader` | Compiled shader module |
| `CreateBuffer` | `Buffer` | GPU buffer (vertex, index, constant, structured, etc.) |
| `CreateBufferView` | `BufferView` | View into a buffer for shader binding |
| `CreateTexture` | `Texture` | GPU texture (2D, 3D, Cube, Array) |
| `CreateTextureView` | `TextureView` | View into a texture for shader binding |
| `CreateSampler` | `Sampler` | Texture sampling and filtering configuration |
| `CreateResourceTable` | `ResourceTable` | Declares and holds resource bindings for shaders |
| `CreateGraphicsPipeline` | `GraphicsPipeline` | Rasterization pipeline |
| `CreateComputePipeline` | `ComputePipeline` | Compute dispatch pipeline |
| `CreateMeshShadingPipeline` | `MeshShadingPipeline` | Mesh shading pipeline |
| `CreateQueryHeap` | `QueryHeap` | GPU query heap for timestamps and occlusion |

## Alignment Constants

Zenith.NET defines alignment constants for cross-platform compatibility:

| Constant | Value | Purpose |
|----------|:-----:|---------|
| `ConstantBufferAlignment` | 256 bytes | Minimum alignment for constant buffer data |
| `TextureRowPitchAlignment` | 256 bytes | Alignment for texture row pitch |
| `TextureDepthPitchAlignment` | 512 bytes | Alignment for 3D texture depth slice pitch |

## Capabilities

Query device features before using optional capabilities:

```csharp
Console.WriteLine($"Device: {context.Capabilities.DeviceName}");

if (context.Capabilities.RayTracingSupported)
{
    // Safe to use acceleration structures and RayQuery
}

if (context.Capabilities.MeshShadingSupported)
{
    // Safe to use mesh shading pipelines
}
```

| Property | Type | Description |
|----------|------|-------------|
| `DeviceName` | `string` | Name of the GPU device |
| `RayTracingSupported` | `bool` | Whether BLAS/TLAS and RayQuery are available |
| `MeshShadingSupported` | `bool` | Whether mesh and amplification shaders are available |

## Validation Messages

When the validation layer is enabled, subscribe to `ValidationMessage` to receive diagnostic messages:

```csharp
context.ValidationMessage += (sender, args) =>
{
    Console.WriteLine($"[{args.Source} - {args.Severity}] {args.Message}");
};
```

| Property | Type | Description |
|----------|------|-------------|
| `Source` | `MessageSource` | `Framework` (Zenith.NET checks) or `GraphicsAPI` (backend driver) |
| `Severity` | `MessageSeverity` | `Error`, `Warning`, or `Message` |
| `Message` | `string` | The diagnostic message text |
| `Timestamp` | `DateTimeOffset` | When the message was generated |
