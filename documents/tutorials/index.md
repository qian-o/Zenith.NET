# Tutorials

Learn Zenith.NET through a sequence of runnable workloads, from a clear-only frame through rasterization, compute, indirect drawing, Ray Tracing, and Mesh Shading.

The complete project lives in [qian-o/ZenithTutorials](https://github.com/qian-o/ZenithTutorials). Tutorial pages load C#, Slang, textures, and screenshots directly from its `master` branch.

## Setup

[Project Setup](project-setup.md) creates the shared window, graphics context, swap chain, frame loop, and renderer contract.

## Rasterization

| Tutorial | Result | New Mechanism |
|----------|--------|---------------|
| [Hello Triangle](rasterization/hello-triangle.md) | One interpolated-color triangle | Vertex input and direct drawing |
| [Textured Quad](rasterization/textured-quad.md) | The tutorial image on an indexed quad | Bindless texture and sampler handles |
| [Spinning Cube](rasterization/spinning-cube.md) | A rotating, depth-tested cube | Constant data, depth, and resize |

## Compute and Indirect Drawing

| Tutorial | Result | New Mechanism |
|----------|--------|---------------|
| [Compute Image Processing](image-processing.md) | A grayscale version of the tutorial image | Storage texture dispatch |
| [Indirect Drawing](indirect-drawing.md) | A 5 by 5 grid of animated cubes | CPU-authored indirect arguments and instance data |

## Advanced Workloads

| Tutorial | Result | Requirement |
|----------|--------|-------------|
| [Ray Tracing](ray-tracing.md) | Three reflective spheres over a checkerboard floor | `RayTracingSupported` |
| [Mesh Shading](mesh-shading.md) | 1,000 sphere instances with GPU culling | `MeshShadingSupported` |
