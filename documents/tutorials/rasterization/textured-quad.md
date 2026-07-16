# Textured Quad

Render the tutorial image on two indexed triangles. This workload adds ImageSharp texture loading, mip levels, a sampler, and bindless resource handles. Start from [Project Setup](../project-setup.md).

![Textured Quad](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/textured-quad.png)

## Indexed Geometry

Four vertices define position and texture coordinates. Six indices reuse them to form two triangles, so the renderer binds both vertex and index buffers before issuing the indexed draw.

The input layout matches the shader's `POSITION0` and `TEXCOORD0` fields. The vertex entry point forwards texture coordinates to the fragment stage without modifying them.

## Bindless Sampling

`LoadTextureFromFile` decodes `Assets/Textures/shoko.png` and generates its mip chain. A linear clamp sampler controls filtering and address behavior.

The constant buffer stores the texture's `SampledHandle` and the sampler's `Handle`. Slang receives those values as typed `DescriptorHandle<Texture2D>` and `DescriptorHandle<SamplerState>` fields, then samples the texture directly.

The ImageSharp loader returns every mip in `Sampled`. Rendering binds those handles directly, clears the drawable, and records one indexed draw.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/TexturedQuadRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/TexturedQuadRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/TexturedQuad.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/TexturedQuad.slang" data-language="slang"></div>

[Texture asset](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Textures/shoko.png)
