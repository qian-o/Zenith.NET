# Image Processing

Convert the tutorial image to grayscale with a compute shader, then display the result. This tutorial introduces storage textures and compute dispatch.

![Compute Image Processing](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/compute-shader.png)

## Create the Resources

Load `shoko.png` as the sampled input. Create a second texture with the same dimensions and `Sampled | Storage` usage for the output.

The constant structure stores the image dimensions, the input sampled handle, the output storage and sampled handles, and a sampler handle. Create one compute pipeline for processing and one graphics pipeline for displaying the result.

## Process the Image

`CSMain` uses 16 by 16 thread groups. Round each image dimension up to that group size and reject threads outside the texture bounds.

The renderer transitions the output from `Undefined` to `Storage`, dispatches the compute pipeline once, then transitions the texture to `Sampled`. The shader reads each source pixel and writes its grayscale value to the output.

## Display the Result

Draw a fullscreen triangle that samples the processed texture. A centered viewport and scissor preserve the image's pixel size when the window is large enough and reduce each dimension to the available drawable size when it is smaller.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/ComputeShader.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/ComputeShader.slang" data-language="slang"></div>