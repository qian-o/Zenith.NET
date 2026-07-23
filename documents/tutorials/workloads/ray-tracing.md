# Ray Tracing

Instead of rasterizing triangles, this tutorial traces rays through a scene: three spheres above a checkerboard floor, with soft shadows and reflections. It runs entirely in a compute shader using inline ray queries, so there is no separate ray-tracing pipeline to manage. It assumes you are comfortable with the compute workflow from [Image Processing](image-processing.md).

![Ray Tracing](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/ray-tracing.png)

> [!NOTE]
> This tutorial requires `App.Context.Capabilities.RayTracingSupported`.

## The Data Layout

Ray tracing needs the scene packed into acceleration structures the GPU can traverse quickly. A bottom-level acceleration structure (BLAS) stores geometry; a top-level acceleration structure (TLAS) stores instances of those BLAS objects. This scene uses a triangle BLAS for the floor, an axis-aligned bounding-box (AABB) BLAS for the three procedural spheres, and one TLAS that references both. The host layouts below define the frame handles and the sphere record used to generate each AABB and test its exact intersection.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-source-region="host-data-layout" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-language="csharp"></div>

See [Ray Tracing](../../docs/workloads/ray-tracing.md) for acceleration-structure lifetime, update, and cross-queue synchronization rules.

## Initialize the Renderer

The constructor checks support, creates the scene buffers and pipelines, builds the acceleration structures, and retains every referenced resource for later frames.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-source-region="initialize-renderer" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-language="csharp"></div>

## Create Scene Buffers

This helper creates the floor vertices and indices, sphere records, and matching AABBs. It uploads all four arrays and returns the buffers with the counts needed by the build descriptions.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-source-region="create-scene-geometry" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-language="csharp"></div>

## Build BLAS and TLAS

The compute command buffer builds both BLAS objects, references them from the TLAS, then submits and waits before the first frame can trace the scene.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-source-region="build-acceleration-structures" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-language="csharp"></div>

## Inline Ray Query

The constant buffer gives the shader its camera, TLAS, sphere records, output image, and sampler handles.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-source-region="ray-tracing-data" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-language="slang"></div>

Each compute thread creates one primary ray and runs its `RayQuery`. Triangle hits shade the floor; procedural candidates read the sphere record, test the exact intersection, and commit valid hits.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-source-region="sphere-intersection" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-language="slang"></div>

After the primary hit, the shading helpers issue additional inline queries for soft shadows and reflections. Their count depends on the shader sampling constants. `CSMain` then tone maps and writes the pixel; the complete shader contains those secondary-ray helpers.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-source-region="trace-primary-rays" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-language="slang"></div>

## Output Texture

The renderer creates or reuses an `R32G32B32A32Float` output texture, updates frame handles, and dispatches enough 16 by 16 thread groups to cover the drawable. `CSMain` rejects threads outside the output dimensions. The result then transitions to `Sampled` and is drawn.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-source-region="render-ray-tracing" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-language="csharp"></div>

The display shader creates a fullscreen triangle and samples the traced image.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-source-region="display-ray-tracing-output" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-language="slang"></div>

`Resize` disposes the output texture. The next frame creates a texture with the current drawable dimensions.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-source-region="resize-output-target" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-language="csharp"></div>

## Full Source

- [Renderer](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs)
- [Shader](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/RayTracing.slang)

## Next

Ray tracing generates images without the graphics pipeline. [Mesh Shading](mesh-shading.md) takes the last step, generating geometry itself on the GPU with the task and mesh stages.