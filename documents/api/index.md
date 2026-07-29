# API Reference

The API reference documents the public Zenith.NET types, members, parameters, and enum values. Start with the [RHI Guide](../docs/index.md) when learning a workflow.

## Start Here

| Type | Purpose |
|------|---------|
| `GraphicsContext` | Creates resources and exposes capabilities and command queues |
| `CommandQueue` | Owns and lends command buffers for one class of GPU work |
| `CommandBuffer` | Queue-owned recorder borrowed for one immediate submission |
| `TimelineValue` | Represents a queue-submission completion point that can be queried or waited on |
| `QueryHeap` / `QueryHeapDesc` | Collect visibility results and GPU timestamps |
| `BufferDesc` / `TextureDesc` | Describe resources before creation |
| `GraphicsPipelineDesc` / `ComputePipelineDesc` | Describe shader pipelines |
| `Surface` / `SwapChain` | Connect rendering to a window |
| `ZenithCompiler` | Compiles Slang source for the selected graphics API |

## Public Namespaces

| Namespace | Contents |
|-----------|----------|
| `Zenith.NET` | Core RHI types and Slang compilation |
| `Zenith.NET.DirectX12` | DirectX 12 context factory |
| `Zenith.NET.Metal` | Metal context factory |
| `Zenith.NET.Vulkan` | Vulkan context factory |
| `Zenith.NET.Extensions.ImageSharp` | Image loading and mip generation |
| `Zenith.NET.Extensions.ImGui` | Dear ImGui integration |
| `Zenith.NET.Views` | Shared View contracts and frame event data |
| `Zenith.NET.Views.*` | Framework-specific View controls |

Browse the namespace tree to inspect the complete public API reference. The guide pages link related concepts, while the [Tutorials](../tutorials/index.md) show the types in complete applications.
