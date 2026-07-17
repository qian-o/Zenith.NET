# Mesh Shading

Render 1,000 sphere instances with task-stage culling and mesh-stage geometry output. This tutorial introduces a mesh shading pipeline and `DispatchMesh`.

![Mesh Shading](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/mesh-shading.png)

> [!NOTE]
> This tutorial requires `App.Context.Capabilities.MeshShadingSupported`.

## Create the Source Mesh

Generate one UV sphere on the CPU with 62 vertices and 120 triangles. Upload both arrays to read-only structured buffers and store their handles in the frame constants.

No vertex or index buffer is bound through the traditional input stage. The mesh shader reads the source geometry through those handles.

## Create the Pipeline

Compile the task, mesh, and fragment entry points from `MeshShading.slang`. Create a mesh shading pipeline with the host color format, a depth format, back-face culling, and read-write depth testing.

## Cull and Emit Instances

The 10 by 10 by 10 grid contains 1,000 possible instances. Per-frame constants provide the view-projection matrix, six frustum planes, light direction, and source-mesh handles.

Call `DispatchMesh(32, 1, 1)` to cover the full grid. Each task workgroup tests up to 32 instance bounds, writes visible IDs into its payload, and dispatches one mesh workgroup for each visible instance. The final group ignores threads beyond the 1,000-instance range.

The mesh stage reads one visible ID, emits the sphere's vertices and triangles, and applies that instance's position and color. The fragment stage shades the generated geometry.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/MeshShading.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/MeshShading.slang" data-language="slang"></div>