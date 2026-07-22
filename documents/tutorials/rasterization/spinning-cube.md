# Spinning Cube

This tutorial turns the flat quad into a rotating 3D cube. It reuses the indexed geometry and constant buffer from [Textured Quad](textured-quad.md), then adds the two things that make 3D work: transform matrices that animate each frame, and a depth buffer so nearer faces hide farther ones. Because the cube can resize with the window, it also handles resize.

![Spinning Cube](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/spinning-cube.png)

## The Data Layout

Each of the cube's eight corners has a position and a color. `Constants` holds the three matrices the vertex shader needs: model, view, and projection. Their explicit byte offsets match the layout the shader expects.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-source-region="host-data-layout" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-language="csharp"></div>

## Build the Depth-Tested Pipeline

The constructor uploads the cube's vertices and indices the same way earlier tutorials did, then builds the pipeline through this helper. Compared with earlier pipelines, it adds a depth format, turns on back-face culling so the inside of the cube is skipped, and enables read-write depth testing.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-source-region="create-depth-pipeline" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-language="csharp"></div>

## Animate the Transform

The host calls `Update` every frame. It advances the rotation, builds the model, view, and projection matrices, and uploads them to the constant buffer. The projection uses the current drawable aspect ratio, so the cube stays correctly proportioned. Slang matrices follow the same row-vector convention as `System.Numerics.Matrix4x4`.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-source-region="update-transforms" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-language="csharp"></div>

## Draw With Depth

`Render` creates the depth texture on the first frame, then clears both the color and depth attachments before drawing. The depth attachment is what lets the cube's front faces correctly cover its back faces.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-source-region="render-with-depth" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-language="csharp"></div>

## Handle Resize

When the window changes size, the depth texture no longer matches the drawable. `Resize` drops it so `Render` recreates it at the new size on the next frame.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-source-region="resize-depth-target" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs" data-language="csharp"></div>

## The Shader

The vertex shader multiplies each position by model, view, and projection in turn and passes the color through. The fragment shader returns the interpolated color.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/SpinningCube.slang" data-source-region="cube-shader" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/SpinningCube.slang" data-language="slang"></div>

## Full Source

- [Renderer](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs)
- [Shader](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/SpinningCube.slang)

## Next

One cube takes one draw call. [Indirect Drawing](indirect-drawing.md) reuses this depth-tested setup to render a thousand instances from a single indirect call, with per-instance data driving each one.
