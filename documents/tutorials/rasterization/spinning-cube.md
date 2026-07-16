# Spinning Cube

Render a rotating indexed cube with per-vertex colors. This workload adds Model-View-Projection data, back-face culling, depth testing, and a depth texture that follows the drawable size. Start from [Project Setup](../project-setup.md).

![Spinning Cube](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/spinning-cube.png)

## Transform Data

Eight vertices and 36 indices describe the cube. The renderer accumulates elapsed time, composes the model rotation, builds the camera view, and recalculates perspective projection from the current drawable aspect ratio.

The three `Matrix4x4` values occupy 192 bytes in one constant buffer. Their row-vector multiplication order in Slang matches `System.Numerics.Matrix4x4`.

## Depth and Resize

The graphics pipeline enables back-face culling and read-write depth testing. Its attachment formats include the swap-chain color format and a `D32FloatS8UInt` depth-stencil format.

Each frame discards the previous color and depth contents, transitions both textures to their attachment layouts, clears them, and records an indexed draw. When the drawable size changes, `Resize` replaces the depth texture with one using the new dimensions.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/SpinningCube.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/SpinningCube.slang" data-language="slang"></div>
