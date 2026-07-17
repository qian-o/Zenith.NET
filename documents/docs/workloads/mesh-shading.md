# Mesh Shading

Mesh shading is an optional graphics path that uses mesh shader workgroups instead of vertex and index input.

## Check Support

Check the capability before creating a mesh shading pipeline:

```csharp
if (!context.Capabilities.MeshShadingSupported)
{
    return;
}
```

## Create the Pipeline

Compile the mesh and fragment entry points. A task shader is optional:

```csharp
string shaderPath = "Assets/Shaders/MeshShading.slang";
using Shader meshShader = context.CreateShader(
    ZenithCompiler.CompileFromFile(context.GraphicsApi, shaderPath, "MSMain"));
using Shader fragmentShader = context.CreateShader(
    ZenithCompiler.CompileFromFile(context.GraphicsApi, shaderPath, "FSMain"));
```

Create a pipeline whose attachment formats match the render pass:

```csharp
using MeshShadingPipeline pipeline = context.CreateMeshShadingPipeline(new()
{
    TaskShader = null,
    MeshShader = meshShader,
    FragmentShader = fragmentShader,
    PrimitiveTopology = PrimitiveTopology.TriangleList,
    AttachmentFormats = new()
    {
        ColorFormats = [PixelFormat.B8G8R8A8SRgb],
        DepthStencilFormat = PixelFormat.D32Float,
        SampleCount = SampleCount.Count1
    },
    RenderState = new()
    {
        Rasterizer = RasterizerState.CullNone(),
        DepthStencil = DepthStencilState.DepthReadWrite(),
        Blend = BlendState.Opaque()
    }
});
```

Set `TaskShader` to a compiled task entry point when the workload uses a task stage.

## Dispatch Mesh Work

Mesh dispatch runs inside a render pass:

```csharp
commandBuffer.Transition(color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
commandBuffer.Transition(depthStencil, default, TextureLayout.Undefined, TextureLayout.DepthStencilAttachment);
commandBuffer.BeginRenderPass(
    [ColorAttachment.Clear(color, clearColor)],
    DepthStencilAttachment.Clear(depthStencil, 1.0f, 0));

commandBuffer.SetPipeline(pipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.DispatchMesh(groupCountX, groupCountY, groupCountZ);

commandBuffer.EndRenderPass();
```

The dispatch counts select mesh shader workgroups. The shader determines how many vertices and primitives each workgroup emits.

## Dispatch Indirectly

`DispatchMeshIndirect` reads one or more `IndirectDispatchMeshArgs` records:

```csharp
commandBuffer.DispatchMeshIndirect(indirectBuffer, offsetInBytes, dispatchCount);
```

Create the argument buffer with `BufferUsages.Indirect`. If earlier GPU work writes the arguments, also add the matching storage usage and record a barrier before dispatch.

See [Bindless Resources](../fundamentals/bindless-resources.md) for shader-visible mesh data and [Synchronization](../fundamentals/synchronization.md) when GPU work produces mesh data or indirect arguments.
