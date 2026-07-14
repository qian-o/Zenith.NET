# Tutorials

These tutorials build one desktop renderer step by step with the current Zenith.NET RHI. The code uses explicit command queues, texture layouts, timeline submission, bindless resource handles, and Slang shaders throughout.

Start with the shared application shell, then follow the subjects in order. Each page contains the complete renderer and shader additions for that stage.

## Getting Started

| Tutorial | You Will Build |
|----------|----------------|
| [Prerequisites](getting-started/prerequisites.md) | A cross-platform window, Graphics API selection, surface, swap chain, synchronous frame loop, and renderer contract |
| [Hello Triangle](getting-started/hello-triangle.md) | A vertex buffer, Slang vertex/fragment shaders, graphics pipeline, render pass, and direct draw |
| [Textured Quad](getting-started/textured-quad.md) | Indexed geometry with an ImageSharp texture, sampler, bindless handles, and constant-buffer binding |
| [Spinning Cube](getting-started/spinning-cube.md) | A depth-tested 3D pipeline with model, view, and projection matrices updated every frame |

## Compute and GPU-Driven Work

| Tutorial | You Will Build |
|----------|----------------|
| [Compute Shader](intermediate/compute-shader.md) | A storage-texture image effect with dispatch sizing and explicit texture transitions |
| [Indirect Drawing](intermediate/indirect-drawing.md) | Compute-generated draw arguments consumed by `DrawIndexedIndirect` after a memory barrier |

## Optional GPU Features

| Tutorial | You Will Build | Capability |
|----------|----------------|------------|
| [Ray Tracing](advanced/ray-tracing.md) | Triangle BLAS, scene TLAS, bindless acceleration-structure access, and inline `RayQuery` | `RayTracingSupported` |
| [Mesh Shading](advanced/mesh-shading.md) | A task/mesh pipeline that emits geometry without vertex or index input | `MeshShadingSupported` |

## Shared Rules

Every tutorial follows the same RHI rules:

1. Obtain command buffers from `GraphicsQueue`, `ComputeQueue`, or `TransferQueue`.
2. Transition textures when their access role changes.
3. Insert `Barrier` when later work depends on earlier writes without a texture layout change.
4. Pass bindless `ResourceHandle` values through explicitly laid-out constant data.
5. Submit recorded commands and use `TimelineValue` for GPU or CPU dependencies.
6. Transition the swap-chain drawable to `Present` before synchronous presentation.
7. Dispose resources deterministically after their final submission completes.

Zenith.NET does not expose frames in flight. The shared application waits for each frame submission before calling `SwapChain.Present()`.

## Requirements

- .NET 10 SDK or later
- DirectX 12 on Windows, Metal 4 on macOS, or Vulkan 1.4 with Zenith.NET's required bindless extensions on Linux
- A GPU and driver that support the selected Graphics API
- Visual Studio, VS Code, or JetBrains Rider

See [Prerequisites](getting-started/prerequisites.md) to create the project and the reusable frame loop.
