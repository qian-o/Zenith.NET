# RHI Guide

Use this guide to learn the Zenith.NET programming model. It covers the C# objects and workflows used to create resources, record GPU work, and present results.

## Fundamentals

Start here if you are new to Zenith.NET:

1. [Runtime](fundamentals/runtime.md) introduces the graphics context, capabilities, queues, and object lifetime.
2. [Commands](fundamentals/commands.md) shows how to record and submit work.
3. [Synchronization](fundamentals/synchronization.md) explains barriers, texture transitions, and queue dependencies.
4. [Shaders](fundamentals/shaders.md) shows how to compile Slang entry points and create shader objects.
5. [Bindless Resources](fundamentals/bindless-resources.md) shows how shaders access resources through handles.
6. [Queries](fundamentals/queries.md) shows how to collect visibility results and measure GPU work.

## Resources

- [Heaps](resources/heaps.md) covers placed resources, allocation requirements, offsets, and lifetime.
- [Buffers](resources/buffers.md) covers creation, data transfer, mapping, and views.
- [Textures](resources/textures.md) covers creation, views, uploads, layouts, resolves, and samplers.

## Workloads

- [Rasterization](workloads/rasterization.md) covers graphics pipelines, render passes, and draw commands.
- [Compute](workloads/compute.md) covers compute pipelines and dispatch commands.
- [Ray Tracing](workloads/ray-tracing.md) covers acceleration structures and inline ray queries.
- [Mesh Shading](workloads/mesh-shading.md) covers mesh shading pipelines and dispatch commands.

## Presentation

- [Swap Chains](presentation/swap-chains.md) shows how to render to a window and present a frame.
- [Views](presentation/views.md) shows how to render through supported .NET UI controls.

Follow the [Tutorials](../tutorials/index.md) to build complete examples. Use the [API Reference](../api/index.md) for exact types and signatures.
