# RHI Guide

This guide defines the Zenith.NET programming model. It explains the contracts shared by DirectX 12, Metal 4, and Vulkan 1.4 without repeating tutorial setup or generated API details.

## Fundamentals

Read these pages in order when learning the RHI:

1. [Runtime and Devices](fundamentals/runtime.md) covers graphics API selection, capabilities, queues, diagnostics, and ownership.
2. [Queues and Commands](fundamentals/commands.md) defines recording, render passes, copies, submission, and timeline values.
3. [Synchronization](fundamentals/synchronization.md) distinguishes memory barriers, texture transitions, and cross-queue waits.
4. [Bindless Resources](fundamentals/bindless-resources.md) defines resource handles, constant data, shader descriptors, and handle lifetime.

## Resources

- [Buffers and Memory](resources/buffers.md) covers usages, residency, views, heaps, uploads, downloads, and mapping.
- [Textures and Sampling](resources/textures.md) covers texture shapes, layouts, views, transfers, resolves, and samplers.

## Workloads

- [Rasterization](workloads/rasterization.md) covers graphics pipelines, attachments, render state, and draw commands.
- [Compute](workloads/compute.md) covers compute pipelines, dispatch sizing, storage access, and indirect dispatch.
- [Ray Tracing](workloads/ray-tracing.md) covers acceleration structures and inline `RayQuery`.
- [Mesh Shading](workloads/mesh-shading.md) covers task and mesh pipelines and dispatch.

## Presentation

- [Surfaces and Swap Chains](presentation/swap-chains.md) defines drawable acquisition, resize, and presentation.
- [View Integrations](presentation/views.md) covers rendering through supported .NET UI controls.

Use the [Tutorials](../tutorials/index.md) for complete applications and the [API Reference](../api/index.md) for exact signatures.
