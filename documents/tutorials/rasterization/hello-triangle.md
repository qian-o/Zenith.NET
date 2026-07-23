# Hello Triangle

This is the first renderer. It draws one triangle whose corner colors blend across the face, and it introduces the pieces every later tutorial reuses: a vertex buffer, a pair of shaders, a graphics pipeline, and a draw call. The host from [Project Setup](../getting-started/project-setup.md) already runs this renderer by default.

![Hello Triangle](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/hello-triangle.png)

## The Vertex Data

A vertex is one corner of the triangle. Here each vertex holds a position and a color. This struct defines that layout on the CPU side.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs" data-source-region="host-data-layout" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs" data-language="csharp"></div>

## Set Up the Renderer

The constructor does the one-time setup. It creates the three vertices and uploads them to a GPU buffer, describes that layout with an `InputLayout` so the pipeline knows how to read each vertex, compiles the vertex and fragment shaders from `HelloTriangle.slang`, and builds a triangle-list pipeline with no culling, no depth testing, and opaque blending.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs" data-source-region="initialize-renderer" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs" data-language="csharp"></div>

## The Shader

The vertex shader outputs each corner's clip-space position, the coordinates the GPU uses to map the triangle onto the screen, and passes its color through. The fragment shader returns that color, and the hardware interpolates it across the triangle to produce the blended look.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/HelloTriangle.slang" data-source-region="triangle-shader" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/HelloTriangle.slang" data-language="slang"></div>

## Draw

The host calls `Render` once per frame. It opens a render pass that clears the drawable, binds the pipeline and vertex buffer, and draws three vertices as one triangle. No index buffer or shader resources are needed yet.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs" data-source-region="render-triangle" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs" data-language="csharp"></div>

## Full Source

- [Renderer](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs)
- [Shader](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/HelloTriangle.slang)

## Next

The triangle's vertices are baked into the shader-facing buffer directly. Next, [Textured Quad](textured-quad.md) reuses this pipeline but feeds it indexed geometry and a sampled texture, introducing the resource-handle model every later tutorial depends on.
