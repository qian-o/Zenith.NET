# Textured Quad

This tutorial draws the shared image on a flat quad. It reuses the pipeline and draw flow from [Hello Triangle](hello-triangle.md) and adds three things: indexed geometry so two triangles can share corners, a loaded texture, and a resource handle that lets the shader read that texture.

![Textured Quad](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/textured-quad.png)

## The Data Layout

A quad has four corners, each carrying a position and a texture coordinate. `Constants` holds two handles the shader uses to reach the texture and the sampler. A handle is a small integer the GPU resolves to the actual resource, so no per-draw binding tables are needed.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/TexturedQuadRenderer.cs" data-source-region="host-data-layout" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/TexturedQuadRenderer.cs" data-language="csharp"></div>

## Set Up the Renderer

The constructor uploads the four vertices and six indices, where the indices reuse corners to form two triangles. It loads `shoko.png` with mipmaps, then writes the texture's sampled handle and the shared sampler's handle into a constant buffer. The pipeline setup matches Hello Triangle, with the input layout now carrying a texture coordinate instead of a color.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/TexturedQuadRenderer.cs" data-source-region="initialize-renderer" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/TexturedQuadRenderer.cs" data-language="csharp"></div>

## The Shader

The shader's constant structure declares the two handles with the same layout as the CPU struct. The fragment shader resolves the handles and samples the texture at the interpolated coordinate.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/TexturedQuad.slang" data-source-region="textured-quad-shader" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/TexturedQuad.slang" data-language="slang"></div>

## Draw

`Render` binds the pipeline, both geometry buffers, and the constant buffer, then issues an indexed draw of six indices.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/TexturedQuadRenderer.cs" data-source-region="render-textured-quad" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/TexturedQuadRenderer.cs" data-language="csharp"></div>

For more on handles, see [Bindless Resources](../../docs/fundamentals/bindless-resources.md).

## Full Source

- [Renderer](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/TexturedQuadRenderer.cs)
- [Shader](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/TexturedQuad.slang)
- [Texture asset](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Textures/shoko.png)

## Next

So far everything is flat and still. [Spinning Cube](spinning-cube.md) takes this textured geometry into 3D, adding transform constants, a depth buffer, back-face culling, and per-frame animation.
