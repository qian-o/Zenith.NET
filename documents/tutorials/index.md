# Tutorials

Build a Zenith.NET application through a sequence of focused, runnable examples. Each tutorial introduces one rendering or compute technique and links to its complete C# and Slang source.

Complete tutorial source is available in [qian-o/ZenithTutorials](https://github.com/qian-o/ZenithTutorials). Each page includes the corresponding source files and rendered result.

## Start Here

[Project Setup](project-setup.md) prepares the shared desktop host and renders a clear-only frame. Complete it before the workload tutorials.

## Rasterization

1. [Hello Triangle](rasterization/hello-triangle.md) introduces vertex input, a graphics pipeline, and direct drawing.
2. [Textured Quad](rasterization/textured-quad.md) adds indexed geometry, texture loading, a sampler, and resource handles.
3. [Spinning Cube](rasterization/spinning-cube.md) adds transformation constants, depth testing, and resize handling.

## GPU Workloads

1. [Image Processing](image-processing.md) writes a texture with a compute shader.
2. [Indirect Drawing](indirect-drawing.md) draws many instances from an argument buffer.
3. [Ray Tracing](ray-tracing.md) builds acceleration structures and traces inline rays.
4. [Mesh Shading](mesh-shading.md) generates and culls geometry with mesh shader workgroups.

Ray Tracing and mesh shading require the corresponding device capability.
