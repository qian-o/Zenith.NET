# Textured Quad

Draw the tutorial image on a quad. This tutorial builds on [Hello Triangle](hello-triangle.md) with indexed geometry, texture loading, a sampler, and shader resource handles.

![Textured Quad](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/textured-quad.png)

## Create Indexed Geometry

Create four vertices containing position and texture coordinates. Six `uint` indices reuse those vertices to form two triangles.

The input layout matches the shader's `POSITION0` and `TEXCOORD0` fields. Bind both buffers and call `DrawIndexed(6, 1, 0, 0, 0)`.

## Load and Bind the Texture

Load `Assets/Textures/shoko.png` with `LoadTextureFromFile` and generate its mip chain. Create a linear clamp sampler for filtering and addressing.

Store `texture.SampledHandle` and `sampler.Handle` in a 16-byte constant structure. The matching Slang structure declares `DescriptorHandle<Texture2D>` and `DescriptorHandle<SamplerState>` fields.

Bind that constant buffer before drawing. The fragment shader samples the texture at the interpolated texture coordinate.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/TexturedQuadRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/TexturedQuadRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/TexturedQuad.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/TexturedQuad.slang" data-language="slang"></div>

[Texture asset](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Textures/shoko.png)
