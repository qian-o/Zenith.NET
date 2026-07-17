# Spinning Cube

Draw a rotating, depth-tested cube. This tutorial builds on indexed rasterization with transformation constants, back-face culling, a depth attachment, and resize handling.

![Spinning Cube](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/spinning-cube.png)

## Update the Transform

Eight vertices and 36 indices describe the cube. `Update` advances the rotation, creates the model and view matrices, and calculates a perspective projection from the current drawable aspect ratio.

Upload the three `Matrix4x4` values to a 192-byte CPU-writable constant buffer. The Slang vertex shader uses the same row-vector convention as `System.Numerics.Matrix4x4`, applying the model, view, and projection transforms in that order.

## Add Depth Testing

Create a `D32FloatS8UInt` depth texture at the drawable size. Add that format to the pipeline, enable back-face culling, and use read-write depth testing.

Each frame transitions and clears both attachments before drawing the cube. When the drawable size changes, `Resize` disposes the old depth texture and creates a replacement with the new dimensions.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/SpinningCube.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/SpinningCube.slang" data-language="slang"></div>
