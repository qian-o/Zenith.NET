# Compute Image Processing

Convert the tutorial image to grayscale with a compute shader, then display the processed texture in the center of the window. Start from [Project Setup](project-setup.md), then choose **Image Processing** from the tutorial selector.

![Compute Shader](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/compute-shader.png)

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/ComputeShader.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/ComputeShader.slang" data-language="slang"></div>

## Compute Pass

The source PNG is loaded as a sampled texture. A second texture with matching dimensions supports both storage writes and sampled reads.

One constant buffer supplies the source sampled handle, output storage handle, output sampled handle, sampler handle, and image dimensions. `CSMain` uses 16 by 16 thread groups, rejects dispatch threads outside the image, reads each source pixel, converts RGB to linear space, computes luminance, and converts the grayscale result back to gamma space.

The renderer calculates group counts by rounding each image dimension up to the thread-group size. It performs the compute dispatch once, then transitions the output texture from `Storage` to `Sampled`.

## Display Pass

A fullscreen-triangle graphics pipeline samples the processed texture without applying another effect. The renderer sets a centered viewport and scissor using the smaller of the image and drawable dimensions, preserving the image's pixel size when the window is larger and clipping it when the window is smaller.