# API Reference

The API reference is generated from the current Zenith.NET source. Use it for exact type, field, method, and enum signatures after learning the RHI model in [Docs](../docs/index.md).

## Namespaces

| Namespace | Description |
|-----------|-------------|
| `Zenith.NET` | Shared RHI types, resource descriptions, pipelines, synchronization, presentation, and Slang compilation |
| `Zenith.NET.DirectX12` | DirectX 12 context creation |
| `Zenith.NET.Metal` | Metal context creation |
| `Zenith.NET.Vulkan` | Vulkan context creation |

## Core Model

| Type | Role |
|------|------|
| `GraphicsContext` | Root object for Graphics API identity, capabilities, queues, validation, and resource creation |
| `GraphicsApi` | Selected graphics API |
| `Capabilities` | Device name plus Ray Tracing and Mesh Shading support |
| `CommandQueue` | Creates command buffers and submits work on one timeline |
| `CommandBuffer` | Records transitions, barriers, copies, render passes, draws, dispatches, queries, and acceleration-structure builds |
| `TimelineValue` | Identifies one queue submission for GPU dependencies or CPU waiting |

Each context exposes `GraphicsQueue`, `ComputeQueue`, and `TransferQueue`.

### Presentation

| Type | Description |
|------|-------------|
| `Surface` | Window-system surface and drawable dimensions |
| `SwapChain` | Owns presentation images and exposes the current `Drawable` texture |
| `SwapChainDesc` | Surface and drawable format |
| `ColorAttachment` | Color attachment plus load, store, subresource, and clear state |
| `DepthStencilAttachment` | Depth/stencil attachment plus load, store, and clear state |

Presentation is synchronous. Render passes receive attachment structs directly.

## Resources and Memory

| Type | Description |
|------|-------------|
| `Buffer` / `BufferView` | Linear storage and typed subranges |
| `BufferDesc` / `BufferUsages` | Size, stride, permitted operations, and memory residency |
| `Texture` / `TextureView` | Formatted multidimensional storage and selected subresource ranges |
| `TextureDesc` / `TextureUsages` | Shape, format, sample count, and permitted operations |
| `TextureLayout` / `TextureSubresource` | Explicit access role and selected mip level or array layer |
| `Sampler` / `SamplerDesc` | Filtering, addressing, comparison, anisotropy, and LOD state |
| `Heap` / `HeapDesc` | Explicit placed-resource allocation |

## Bindless Access

Buffers, textures, views, samplers, and top-level acceleration structures expose `ResourceHandle` values for Slang `DescriptorHandle<T>` declarations. See [Bindless Resources](../docs/fundamentals/bindless-resources.md).

## Pipelines

| Type | Description |
|------|-------------|
| `Shader` / `ShaderDesc` | Compiled Graphics API shader object and entry metadata |
| `ZenithCompiler` | Compiles Slang source or files for the selected `GraphicsApi` |
| `GraphicsPipeline` / `GraphicsPipelineDesc` | Vertex/fragment rasterization pipeline |
| `ComputePipeline` / `ComputePipelineDesc` | Compute pipeline |
| `MeshShadingPipeline` / `MeshShadingPipelineDesc` | Optional task plus required mesh/fragment pipeline |

## Ray Tracing

| Type | Description |
|------|-------------|
| `RayTracingGeometry` | Triangle or AABB BLAS geometry |
| `BottomLevelAccelerationStructure` | BLAS built by a command buffer |
| `RayTracingInstance` | BLAS transform, identity, mask, and instance flags |
| `TopLevelAccelerationStructure` | TLAS built from BLAS instances and exposed through a bindless handle |

Ray Tracing in the current RHI combines BLAS/TLAS with inline Slang `RayQuery`.

## Queries

| Type | Description |
|------|-------------|
| `QueryHeap` / `QueryHeapDesc` | Occlusion, binary occlusion, or timestamp query storage |

## Extensions

| Namespace | Description |
|-----------|-------------|
| `Zenith.NET.Extensions.ImageSharp` | Texture loading from ImageSharp streams and files |
| `Zenith.NET.Extensions.ImGui` | Dear ImGui rendering integration |
| `Zenith.NET.Extensions.Skia` | Skia rendering integration |

## Views

| Namespace | Description |
|-----------|-------------|
| `Zenith.NET.Views` | Shared view contract, frame scheduling, and drawable event data |
| `Zenith.NET.Views.Avalonia` | Avalonia integration |
| `Zenith.NET.Views.Maui` | .NET MAUI integration |
| `Zenith.NET.Views.WinForms` | Windows Forms integration |
| `Zenith.NET.Views.WinUI` | WinUI 3 and Uno integration |
| `Zenith.NET.Views.WPF` | WPF integration |

## Navigation

Browse the generated namespace tree for complete signatures. Start with `GraphicsContext`, continue with `CommandBuffer`, then inspect the resource and pipeline descriptions used by your workload.
