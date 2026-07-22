# Tutorials

These tutorials teach Zenith.NET by building real workloads with Slang shaders. Every page maps to a renderer in the runnable [ZenithTutorials](https://github.com/qian-o/ZenithTutorials) repository, so you can read the explanation here and run the exact same code locally.

## How the Tutorials Work

The tutorial repository ships one desktop host application and one renderer per tutorial. The host owns everything a renderer needs to run: the graphics context, the window, the frame loop, and the shared assets. You do this setup once in [Project Setup](getting-started/project-setup.md). After that, each tutorial adds only the renderer for its topic and never repeats host plumbing.

## Learning Path

Do the setup once, then follow Rasterization in order. Each page builds on the one before it, so by the end you have written every stage of a renderer yourself. The Workloads pages branch off from there into compute, ray tracing, and mesh shading.

### Getting Started

Run the host once so every later renderer has a window, a device, and shared assets ready to go.

- [Project Setup](getting-started/project-setup.md) — Clone, build, and run the sample host.

### Rasterization

The classic graphics pipeline, built up one concept at a time from a single triangle to a grid of instanced objects.

1. [Hello Triangle](rasterization/hello-triangle.md) — Your first pixels: vertex input, a pipeline, and a draw call.
2. [Textured Quad](rasterization/textured-quad.md) — Indexed geometry plus a sampled texture and resource handles.
3. [Spinning Cube](rasterization/spinning-cube.md) — Motion in 3D with transform constants, culling, and depth testing.
4. [Indirect Drawing](rasterization/indirect-drawing.md) — A grid of instanced cubes from a single indirect, instanced draw call.

### Workloads

GPU techniques beyond the fixed graphics pipeline.

5. [Image Processing](workloads/image-processing.md) — Step off the graphics pipeline: a compute shader per pixel.
6. [Ray Tracing](workloads/ray-tracing.md) — Trace rays for real shadows and reflections with inline ray queries.
7. [Mesh Shading](workloads/mesh-shading.md) — Generate geometry on the GPU with the task and mesh stages.

Later pages assume the resource-handle model from Textured Quad. Indirect Drawing reuses the depth-tested setup from Spinning Cube, and Ray Tracing builds on the compute workflow from Image Processing. Skip Ray Tracing or Mesh Shading if your device does not report the matching capability.

For any API concept a tutorial does not cover in depth, see the [Zenith.NET documentation](../docs/index.md).
