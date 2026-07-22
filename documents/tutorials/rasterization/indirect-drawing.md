# Indirect Drawing

This tutorial draws a 5 by 5 grid of 25 rotating cubes, but records only one draw command to do it. The idea is that the GPU reads the draw parameters and per-cube data from buffers, so the same command can produce many objects. It builds directly on the depth-tested cube from [Spinning Cube](spinning-cube.md).

![Indirect Drawing](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/indirect-drawing.png)

## The Data Layout

The GPU reads two kinds of records: one indirect draw command shared by the whole frame, and one `Instance` record per cube that the shader selects with `SV_InstanceID`. The host declarations below show the shared vertex format, the 80-byte instance record, and the constants that carry the instance-buffer handle.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-source-region="host-data-layout" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-language="csharp"></div>

The constructor keeps the same cube geometry and depth pipeline as the previous tutorial, then adds the indirect, instance, and constant buffers shown below. For the API requirements behind indirect draws, see [Rasterization](../../docs/workloads/rasterization.md).

## Create the Indirect Command

The helper fills one complete `IndirectDrawIndexedArgs` record, uploads it to an indirect buffer, and returns the buffer retained by the renderer.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-source-region="create-indirect-buffer" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-language="csharp"></div>

## Instance Data

`Update` calculates the grid position, rotation, and color for all 25 cubes, then uploads the array of 80-byte records.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-source-region="update-instances" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-language="csharp"></div>

The shader uses `SV_InstanceID` to index the structured buffer, transforms the shared cube geometry with that instance matrix, and applies its color.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/IndirectDrawing.slang" data-source-region="indirect-drawing-shader" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/IndirectDrawing.slang" data-language="slang"></div>

## Record the Draw

`Render` binds the shared cube geometry and constants, then asks the GPU to execute one record from the indirect buffer. That record expands to 25 indexed instances.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-source-region="render-indirect" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-language="csharp"></div>

The renderer uses a depth attachment sized to the drawable. Resizing updates the projection constants and invalidates the depth texture for recreation on the next frame.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-source-region="resize-indirect-resources" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-language="csharp"></div>

## Full Source

- [Renderer](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs)
- [Shader](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/IndirectDrawing.slang)

## Next

That completes the graphics pipeline path. [Image Processing](../workloads/image-processing.md) steps off it entirely, running a compute shader with one thread per pixel instead of drawing geometry.