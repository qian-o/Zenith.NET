# Ray Tracing

Render three spheres above a checkerboard floor with inline ray queries. This tutorial introduces acceleration structures and `RayQuery` in a compute pipeline.

![Ray Tracing](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/ray-tracing.png)

> [!NOTE]
> This tutorial requires `App.Context.Capabilities.RayTracingSupported`.

## Build the Scene

Create two bottom-level acceleration structures (BLAS):

- The floor BLAS contains two indexed triangles.
- The sphere BLAS contains three axis-aligned bounding boxes.

Create one top-level acceleration structure (TLAS) with an instance of each BLAS. Record all three builds on the compute queue and wait before tracing.

The sphere records remain in a structured buffer. Their bounding boxes identify candidates, while the shader performs the exact sphere intersection and commits accepted procedural hits.

## Trace the Frame

Create a floating-point texture with `Sampled | Storage` usage. Each frame, update constants containing the camera position, TLAS handle, sphere-buffer handle, output handles, and sampler handle.

Dispatch `CSMain` over the drawable dimensions. The shader traces the same TLAS for primary, shadow, and reflection rays, then writes the final color to the storage texture.

## Display and Resize

Transition the output texture from `Storage` to `Sampled` and draw it with a fullscreen triangle. `Resize` replaces the output texture so the dispatch dimensions continue to match the drawable.

Keep both BLAS objects alive until after the TLAS is no longer used.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-language="slang"></div>