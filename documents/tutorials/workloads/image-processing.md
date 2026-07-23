# Image Processing

This is the first tutorial that leaves rasterization behind. Instead of drawing geometry, a compute shader runs one thread per pixel to convert the shared image to grayscale, and a small graphics pass then displays the result. It reuses the image loading and resource-handle model from [Textured Quad](../rasterization/textured-quad.md).

![Compute Image Processing](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/compute-shader.png)

## The Data Layout

There are two passes over a single output texture. The compute pass writes it as a `Storage` image; the graphics pass then reads it as a `Sampled` texture and draws it on a fullscreen triangle, one oversized triangle that covers the whole screen so a single draw samples every pixel. Because the same texture is first written then sampled, it changes state between the two passes. The constant structure below carries the image dimensions and the resource handles both passes share.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs" data-source-region="host-data-layout" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs" data-language="csharp"></div>

For background, see [Compute](../../docs/workloads/compute.md) for dispatch basics and [Bindless Resources](../../docs/fundamentals/bindless-resources.md) for typed texture handles.

## Initialize Both Passes

The constructor loads the source image, creates the dual-use output texture, writes all resource handles into one constant buffer, and creates the compute and display pipelines.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs" data-source-region="initialize-renderer" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs" data-language="csharp"></div>

## Process and Display Pixels

`CSMain` rejects excess threads, converts one source pixel to grayscale, and writes one output pixel. `VSMain` creates a fullscreen triangle from `SV_VertexID`; `FSMain` samples the processed image.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/ComputeShader.slang" data-source-region="image-processing-shader" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/ComputeShader.slang" data-language="slang"></div>

## Record the Two Passes

On the first frame, `Render` transitions the output to `Storage` and dispatches workgroups whose size is 16 by 16 threads. The group counts round each image dimension up by 16. After the dispatch, the output transitions to `Sampled`. Every frame then centers the result and records the fullscreen draw.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs" data-source-region="process-and-display" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs" data-language="csharp"></div>

## Full Source

- [Renderer](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs)
- [Shader](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/ComputeShader.slang)
- [Texture asset](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Textures/shoko.png)

## Next

Compute shaders can do far more than filter an image. [Ray Tracing](ray-tracing.md) uses the same compute workflow to trace rays through a scene, producing real shadows and reflections with inline ray queries.