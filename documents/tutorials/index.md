# Tutorials

The Zenith.NET tutorials examine complete rendering workloads built with C# and the Slang shader language. Each guide focuses on the resources, pipelines, commands, data contracts, and lifetime rules that connect a workload to Zenith.NET.

Begin with Project Setup and Application Host. The six guides then move from a first graphics pipeline to compute, indirect drawing, inline ray queries, and programmable geometry.

> [!NOTE]
> The guides explain the Zenith.NET integration points in detail. Supporting graphics techniques such as sampling patterns, tone mapping, and procedural mesh generation are summarized when they are not essential to the API workflow. Their complete implementations remain available in the tutorial source.

## Getting started

- [Project Setup](getting-started/project-setup.md) creates the .NET 10 project and configures runtime assets.
- [Application Host](getting-started/application-host.md) follows the cross-platform window, swap chain, frame loop, and offscreen texture presenter.

## Guides

| Guide | Result | Focus |
| --- | --- | --- |
| [Hello Triangle](guides/hello-triangle.md) | A vertex-colored triangle | Vertex input, graphics pipeline, render pass, and draw command |
| [Spinning Cube](guides/spinning-cube.md) | A rotating depth-tested cube | Indexed geometry, constant buffers, transforms, depth, and resize |
| [Compute Shader](guides/compute-shader.md) | A grayscale image | Storage textures, descriptor handles, dispatch, and layout transitions |
| [Indirect Drawing](guides/indirect-drawing.md) | A grid of animated cubes | Structured instance data and indirect draw arguments |
| [Ray Tracing](guides/ray-tracing.md) | Procedural spheres above a floor | Acceleration structures, inline ray queries, and compute output |
| [Mesh Shading](guides/mesh-shading.md) | A culled sphere grid | Task and mesh shaders, payload compaction, and mesh dispatch |

The complete, runnable implementations are maintained in the [ZenithTutorials repository](https://github.com/qian-o/ZenithTutorials). Code shown in these pages is static so it remains readable and searchable with the rest of the documentation; each guide links its complete renderer and shader at the end.

## Suggested paths

Complete [Hello Triangle](guides/hello-triangle.md) first. From there:

- continue with [Spinning Cube](guides/spinning-cube.md) and [Indirect Drawing](guides/indirect-drawing.md) for the graphics path;
- continue with [Compute Shader](guides/compute-shader.md) and [Ray Tracing](guides/ray-tracing.md) for compute-produced images;
- read [Mesh Shading](guides/mesh-shading.md) after Spinning Cube when you are comfortable with graphics pipelines, depth attachments, and GPU thread groups.

Ray tracing and mesh shading are optional device capabilities. Their guides show how to test support before creating workload-specific resources.
