# Documentation

Zenith.NET is an explicit, bindless rendering hardware interface for .NET. It presents one strongly typed model across DirectX 12, Metal 4, and Vulkan 1.4 while keeping queues, memory residency, synchronization, and resource state under application control.

These pages explain that model. For task-oriented, runnable examples, follow the [Tutorials](../tutorials/index.md).

## Design Boundaries

Zenith.NET exposes the common modern GPU model shared by its three graphics APIs. It intentionally avoids graphics API-specific feature branches in application code.

The public capability surface contains two optional feature checks:

- Ray Tracing, implemented through acceleration structures and inline `RayQuery`.
- Mesh Shading, implemented through task and mesh shader pipelines.

Swap-chain presentation is synchronous. Zenith.NET does not expose a frames-in-flight model.

## Core Model

| Topic | Description |
|-------|-------------|
| [Graphics Context](concepts/graphics-context.md) | Graphics API creation, queues, capabilities, validation, ownership, and resource factories |
| [Commands](concepts/command-model.md) | Command buffer recording, render passes, dispatches, copies, queries, and submission |
| [Synchronization and Barriers](concepts/synchronization.md) | Memory barriers, texture transitions, timeline values, and cross-queue dependencies |
| [Bindless Resources](concepts/resource-binding.md) | Resource handles, explicitly laid-out constant data, and Slang `DescriptorHandle<T>` |

## Resources

| Topic | Description |
|-------|-------------|
| [Buffers and Memory](resources/buffers.md) | Buffer usages, memory residency, views, heaps, uploads, and downloads |
| [Textures and Views](resources/textures.md) | Texture types, usages, subresources, views, layouts, and transfers |
| [Samplers](resources/samplers.md) | Filtering, address modes, comparison sampling, anisotropy, and LOD |

## Rendering

| Topic | Description |
|-------|-------------|
| [Graphics Pipelines](features/graphics.md) | Rasterization pipelines, render state, attachments, draws, viewports, and scissors |
| [Compute and Indirect](features/compute.md) | Compute pipelines, dispatch sizing, storage resources, barriers, and indirect commands |
| [Ray Tracing](features/ray-tracing.md) | BLAS/TLAS creation, updates, bindless scene access, and inline `RayQuery` |
| [Mesh Shading](features/mesh-shading.md) | Task and mesh shader pipelines, capability checks, and direct or indirect dispatch |

## Presentation and Integration

| Topic | Description |
|-------|-------------|
| [Graphics API Selection](platform/backend-selection.md) | Runtime selection for DirectX 12, Metal 4, and Vulkan 1.4 |
| [Surfaces and Swap Chains](platform/presentation.md) | Native surfaces, drawables, layout transitions, resizing, and synchronous presentation |
| [UI Framework Integration](platform/ui-frameworks.md) | Zenith views for Avalonia, MAUI, WinForms, WinUI, and WPF |

## Guidance

[Best Practices](best-practices.md) covers ownership, synchronization scope, batching, validation, data layout, resizing, and deterministic disposal.

## Next Steps

- Start with [Graphics Context](concepts/graphics-context.md) to understand object ownership and queue access.
- Continue with [Synchronization and Barriers](concepts/synchronization.md) before building multi-pass GPU workloads.
- Use the [API Reference](../api/index.md) for complete type and member signatures.
