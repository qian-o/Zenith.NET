# Mesh Shading

Render 1,000 instances of one sphere mesh with task-shader frustum culling and mesh-shader geometry output. The CPU uploads the source mesh; the GPU selects visible instances and emits their vertices and triangles. Start from [Project Setup](project-setup.md).

![Mesh Shading](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/mesh-shading.png)

> [!NOTE]
> This tutorial requires `App.Context.Capabilities.MeshShadingSupported`.

## Workload

| Stage | Work |
|-------|------|
| Task | Tests 32 instances against six frustum planes and compacts visible IDs into a payload |
| Mesh | Reads one visible instance, emits 62 vertices and 120 triangles, and applies its grid position and color |
| Fragment | Applies ambient and directional diffuse lighting |

The 10 by 10 by 10 grid produces 1,000 possible instances. Only IDs emitted by the task stage reach the mesh stage.

## Mesh Data and Dispatch

The renderer generates one 62-vertex, 120-triangle UV sphere and uploads the two arrays once. Bindless structured-buffer handles expose that source mesh to the shader.

Per-frame constants contain the view-projection matrix, six frustum planes, light direction, and both mesh handles. `DispatchMesh(32, 1, 1)` launches enough task groups to cover all 1,000 instances. Each task group dispatches mesh work only for the visible IDs it collected.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/MeshShading.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/MeshShading.slang" data-language="slang"></div>