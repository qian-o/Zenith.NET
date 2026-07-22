# Mesh Shading

This final tutorial replaces the fixed vertex and index input of rasterization with a programmable geometry pipeline. A task stage decides which of 1,000 sphere instances are visible, and a mesh stage generates the geometry for each survivor on the GPU. This is the most advanced tutorial and assumes you are comfortable with the depth-tested cube from [Spinning Cube](../rasterization/spinning-cube.md) and per-instance data from [Indirect Drawing](../rasterization/indirect-drawing.md).

![Mesh Shading](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/mesh-shading.png)

> [!NOTE]
> This tutorial requires `App.Context.Capabilities.MeshShadingSupported`.

## The Data Layout

The task stage filters work and passes a small payload to the mesh stage. The mesh stage then emits vertices and primitives directly, without any traditional vertex or index input. Here the CPU uploads one sphere mesh once, the task shader picks the visible instances, and the mesh shader emits that sphere at each selected grid position. The host source layouts below match the structured buffers the mesh shader reads.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-source-region="host-source-layout" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-language="csharp"></div>

See [Mesh Shading](../../docs/workloads/mesh-shading.md) for the pipeline and dispatch model.

## Create the Source Mesh

`CreateSphereGeometry` returns a complete 62-vertex, 120-triangle UV sphere. The constructor uploads both arrays to read-only storage buffers and creates the remaining renderer resources.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-source-region="create-source-geometry" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-language="csharp"></div>

The constructor uploads the returned arrays and passes their storage handles to the per-frame constant buffer.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-source-region="initialize-renderer" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-language="csharp"></div>

## Mesh Shading Pipeline

The pipeline compiles all three stages, sets the task and mesh thread-group sizes expected by the shader, and enables back-face culling with depth testing.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-source-region="create-mesh-shading-pipeline" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-language="csharp"></div>

## Cull Instances

The 10 by 10 by 10 grid contains 1,000 possible instances. `Update` calculates the camera, extracts six frustum planes, and uploads those planes with the light and source-buffer handles.

The host frame structure fixes the matrix, plane, light, and resource-handle offsets consumed by Slang.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-source-region="host-frame-layout" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-language="csharp"></div>

`Update` fills that structure from the current camera and the two source buffers, then uploads it for the task, mesh, and fragment stages.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-source-region="update-frame-data" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-language="csharp"></div>

The shader data contract defines the source layouts, task payload, vertex output, frame constants, and resource handles used across all three stages.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/MeshShading.slang" data-source-region="mesh-shader-data" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/MeshShading.slang" data-language="slang"></div>

The task stage maps IDs to grid positions, tests each bounding sphere against the six planes, compacts up to 32 visible IDs into shared payload, and dispatches one mesh workgroup per visible instance.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/MeshShading.slang" data-source-region="task-shader" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/MeshShading.slang" data-language="slang"></div>

## Emit Geometry

The mesh stage reads one payload ID, emits the source sphere at that instance position, and writes all triangle indices. The fragment stage lights the generated vertices.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/MeshShading.slang" data-source-region="mesh-and-fragment-shaders" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/MeshShading.slang" data-language="slang"></div>

The renderer calls `DispatchMesh(32, 1, 1)` to cover all instances. Its depth attachment is recreated with the current drawable dimensions after a resize.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-source-region="record-mesh-draw" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-language="csharp"></div>

After a resize, the renderer releases only the size-dependent depth target; the next `Render` call recreates it.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-source-region="resize-render-targets" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-language="csharp"></div>

## Full Source

- [Renderer](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs)
- [Shader](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/MeshShading.slang)

## Next

That is the full set of tutorials, from a single triangle to GPU-generated geometry. For deeper coverage of any API used along the way, see the [Zenith.NET documentation](../../docs/index.md).