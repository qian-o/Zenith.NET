# Hello Triangle

Draw one triangle with interpolated vertex colors. This tutorial adds a vertex buffer, vertex and fragment shaders, a graphics pipeline, and a direct draw to [Project Setup](../project-setup.md).

![Hello Triangle](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/hello-triangle.png)

## Define the Vertices

Create three vertices. Each vertex stores a `Vector3` position followed by a `Vector4` color. Upload the array to a GPU-only buffer with `Vertex | TransferDst` usage.

Build an `InputLayout` with matching `Position` and `Color` elements. The layout calculates a 28-byte stride from those formats.

## Create the Pipeline

Compile `VSMain` and `FSMain` from `HelloTriangle.slang`. The vertex shader writes clip-space position and forwards color; the fragment shader returns the interpolated color.

Create a triangle-list pipeline with the input layout, the host color format, no culling, no depth testing, and opaque blending.

## Draw the Triangle

In `Render`, transition the drawable to `ColorAttachment`, clear it, bind the pipeline and vertex buffer, and call `Draw(3, 1, 0, 0)`.

No index buffer, constant buffer, or shader resource is needed for this example.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/HelloTriangle.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/HelloTriangle.slang" data-language="slang"></div>
