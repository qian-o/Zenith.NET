# Indirect Drawing

Draw a 5 by 5 grid of rotating cubes with one indirect command. This tutorial builds on [Spinning Cube](rasterization/spinning-cube.md) with an argument buffer and per-instance data.

![Indirect Drawing](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/indirect-drawing.png)

## Create the Argument Buffer

Reuse the cube's eight vertices and 36 indices. Create a buffer with `BufferDesc.Indirect` and upload one `IndirectDrawIndexedArgs` record:

- `IndexCount` is 36.
- `InstanceCount` is 25.
- The remaining offsets start at zero.

The record is initialized once. `DrawIndexedIndirect` reads it when the command executes.

## Update Instance Data

Create a CPU-writable structured buffer containing one model matrix and color for each cube. On every update, calculate the grid position and rotation for all 25 instances and upload the records.

Store the camera matrices and `instanceBuffer.StorageReadOnlyHandle` in the constant buffer. The vertex shader uses `SV_InstanceID` to read the matching `InstanceData` record.

## Draw Indirectly

Keep the depth attachment and pipeline from the cube example. Replace the direct indexed draw with:

```csharp
commandBuffer.DrawIndexedIndirect(indirectBuffer, 0, 1);
```

The final argument selects one indirect record, which draws 25 instances.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/IndirectDrawing.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/IndirectDrawing.slang" data-language="slang"></div>