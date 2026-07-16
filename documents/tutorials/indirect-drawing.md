# Indirect Drawing

Render a 5 by 5 grid of independently rotating cubes with one `DrawIndexedIndirect` command. The CPU initializes the draw arguments and updates the instance data; the GPU reads both when it executes the draw. Start from [Project Setup](project-setup.md).

![Indirect Drawing](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/indirect-drawing.png)

## Indirect Arguments

The cube geometry uses the same eight vertices and 36 indices as the spinning-cube workload. A separate indirect buffer stores `IndirectDrawIndexedArgs` with an index count of 36 and an instance count of 25.

The CPU initializes that argument record once. During rendering, `DrawIndexedIndirect` reads it from the buffer instead of receiving draw dimensions as direct command parameters.

## Instance Data

A CPU-writable structured buffer contains one model matrix and color for every cube. Each update calculates a grid position and an independent rotation, then uploads all 25 records.

The constant buffer supplies the camera matrices and the instance buffer's `StorageReadOnlyHandle`. In Slang, `SV_InstanceID` indexes the typed `StructuredBuffer<InstanceData>` and selects the transform and color for the current instance.

The workload retains the depth attachment and resize behavior introduced by [Spinning Cube](rasterization/spinning-cube.md), while replacing its direct indexed draw with one indirect, instanced command.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/IndirectDrawing.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/IndirectDrawing.slang" data-language="slang"></div>