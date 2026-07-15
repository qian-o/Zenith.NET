# Hello Triangle

Replace the clear-only workload with the smallest complete graphics path: one vertex buffer, two Slang entry points, one graphics pipeline, and one direct draw. Start from [Project Setup](../project-setup.md), then choose **Hello Triangle** from the tutorial selector.

![Hello Triangle](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/hello-triangle.png)

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/HelloTriangle.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/HelloTriangle.slang" data-language="slang"></div>

## Vertex Input

The renderer uploads three vertices, each containing a `Vector3` position followed by a `Vector4` color. The `InputLayout` declares matching `Position` and `Color` elements, producing a 28-byte stride.

The Slang vertex entry point receives those attributes and writes clip-space position. Its color output is interpolated across the triangle and returned by the fragment entry point.

## Graphics Pipeline

The pipeline uses triangle-list topology, no culling, no depth testing, and an opaque blend state. Its attachment format matches the swap-chain color format and sample count supplied by the shared host.

During rendering, the command buffer transitions the drawable to `ColorAttachment`, begins a clearing render pass, binds the pipeline and vertex buffer, and records one direct draw with three vertices and one instance.
