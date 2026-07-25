# Mesh Shading

Mesh shading is an optional graphics path that uses mesh shader workgroups instead of vertex and index input.

## Check Support

Check `context.Capabilities.MeshShadingSupported` before creating a mesh shading pipeline.

## Create the Pipeline

Compile the mesh and fragment entry points for the active context. Compile a task entry point only when the workload uses a task stage.

Create a pipeline from a description whose attachment formats match the render pass:

```csharp
MeshShadingPipelineDesc desc = new()
{
    TaskShader = null,
    MeshShader = meshShader,
    FragmentShader = fragmentShader,
    PrimitiveTopology = PrimitiveTopology.TriangleList,
    AttachmentFormats = attachmentFormats,
    RenderState = renderState
};

MeshShadingPipeline pipeline = context.CreateMeshShadingPipeline(desc);
```

Set `TaskShader` to a compiled task entry point when the workload uses a task stage.

## Dispatch Mesh Work

Mesh dispatch runs inside a render pass:

```csharp
commandBuffer.Transition(texture, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
commandBuffer.BeginRenderPass([ColorAttachment.Clear(texture, default)], null);

commandBuffer.SetPipeline(pipeline);
commandBuffer.DispatchMesh(groupCountX, groupCountY, groupCountZ);

commandBuffer.EndRenderPass();
```

Without a task stage, the dispatch counts select mesh shader workgroups directly. When `TaskShader` is present, they select task shader workgroups, and each task workgroup determines how many mesh shader workgroups to launch. Each mesh shader workgroup determines how many vertices and primitives it emits.

## Dispatch Indirectly

`DispatchMeshIndirect` reads one or more `IndirectDispatchMeshArgs` records. Its counts follow the same task-versus-mesh workgroup interpretation as `DispatchMesh`.

Create the argument buffer with `BufferUsages.Indirect`. If earlier GPU work writes the arguments, also add the matching storage usage and record a barrier before dispatch.

See [Bindless Resources](../fundamentals/bindless-resources.md) for shader-visible mesh data and [Synchronization](../fundamentals/synchronization.md) when GPU work produces mesh data or indirect arguments.
