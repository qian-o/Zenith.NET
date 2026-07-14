# Mesh Shading

This tutorial renders one triangle with the smallest complete mesh shading workload: a mesh shader generates all geometry, a fragment shader colors it, and `DispatchMesh` launches one mesh workgroup. No vertex buffer, index buffer, constant buffer, or shader-visible resource is needed.

Use the application shell from [Prerequisites](../getting-started/prerequisites.md). `App` supplies `IRenderer.Render` with a command buffer and the current swap-chain drawable. The renderer records the drawable's transition into color-attachment use and its rendering commands; `App` records the final transition to `Present`, calls `Submit().Wait()`, and then calls `SwapChain.Present()`.

## Device Capability

Mesh shading is optional, so check the selected device before compiling shaders or creating the pipeline:

```csharp
if (!App.Context.Capabilities.MeshShadingSupported)
{
    throw new PlatformNotSupportedException("Mesh Shading is not supported by the selected device.");
}
```

`MeshShadingSupported` is the public runtime capability gate. Each Graphics API implementation derives it from the selected device: the DirectX 12 mesh shader tier, the required Metal GPU family, or Vulkan mesh-shader extension availability. Do not infer support from the operating system or `GraphicsApi` alone.

The current pipeline terminology is:

| Stage | Required | Role |
|-------|----------|------|
| task | No | Selects or expands mesh work and can pass a payload to mesh workgroups |
| mesh | Yes | Produces vertices and primitive indices |
| fragment | Yes | Shades rasterized fragments |

This example intentionally omits the task stage. A task shader is useful when culling or selecting many meshlets, but it adds no value when one dispatch always emits one triangle.

## Shader

Create `Assets/Shaders/MeshShading.slang`:

```slang
struct MeshOutput
{
    float4 Position : SV_POSITION;

    float3 Color : COLOR0;
};

[shader("mesh")]
[numthreads(1, 1, 1)]
[outputtopology("triangle")]
void MSMain(out vertices MeshOutput vertices[3], out indices uint3 triangles[1])
{
    SetMeshOutputCounts(3, 1);

    vertices[0].Position = float4(0.0, 0.65, 0.0, 1.0);
    vertices[0].Color = float3(1.0, 0.2, 0.15);

    vertices[1].Position = float4(0.6, -0.5, 0.0, 1.0);
    vertices[1].Color = float3(0.15, 0.85, 0.35);

    vertices[2].Position = float4(-0.6, -0.5, 0.0, 1.0);
    vertices[2].Color = float3(0.2, 0.45, 1.0);

    triangles[0] = uint3(0, 1, 2);
}

[shader("fragment")]
float4 FSMain(MeshOutput input) : SV_TARGET
{
    return float4(input.Color, 1.0);
}
```

`MSMain` uses one thread because there are only three fixed vertices to write. `SetMeshOutputCounts(3, 1)` declares the output size before the shader fills the three vertex records and one triangle index triplet. `SV_POSITION` carries clip-space position into rasterization, while `COLOR0` is interpolated for `FSMain`.

## Renderer

Create `Renderers/MeshShadingRenderer.cs`:

```csharp
namespace ZenithTutorials.Renderers;

internal sealed class MeshShadingRenderer : IRenderer
{
    private readonly MeshShadingPipeline pipeline;

    public MeshShadingRenderer()
    {
        if (!App.Context.Capabilities.MeshShadingSupported)
        {
            throw new PlatformNotSupportedException("Mesh Shading is not supported by the selected device.");
        }

        ShaderDesc meshShaderDesc = ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("MeshShading.slang"), "MSMain");
        meshShaderDesc.ThreadGroupSize = new() { X = 1, Y = 1, Z = 1 };

        using Shader meshShader = App.Context.CreateShader(meshShaderDesc);
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("MeshShading.slang"), "FSMain"));

        pipeline = App.Context.CreateMeshShadingPipeline(new()
        {
            TaskShader = null,
            MeshShader = meshShader,
            FragmentShader = fragmentShader,
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [App.ColorFormat],
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthNone(),
                Blend = BlendState.Opaque()
            }
        });
    }

    public void Update(double deltaTime)
    {
    }

    public void Render(CommandBuffer commandBuffer, Texture drawable)
    {
        commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.Clear(drawable, new(0.04f, 0.055f, 0.075f, 1.0f))], null);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.DispatchMesh(1, 1, 1);

        commandBuffer.EndRenderPass();
    }

    public void Resize(uint width, uint height)
    {
    }

    public void Dispose()
    {
        pipeline.Dispose();
    }
}
```

## Run

Replace `Program.cs`:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

App.Run<MeshShadingRenderer>();
```

Run the project:

```bash
dotnet run
```

On a supported device, the window displays a three-color triangle over the clear color. On an unsupported device, renderer construction stops at the capability check before any mesh shading object is created.

## How It Works

The pipeline descriptor matches the public `MeshShadingPipelineDesc`: an optional `TaskShader`, required mesh and fragment shaders, triangle topology, attachment formats compatible with the drawable, and rasterization state. There is no depth attachment, so depth testing is disabled.

`ShaderDesc.ThreadGroupSize` must match the mesh entry point's `[numthreads]` values. The current Slang reflection path reports compute thread-group sizes but does not report them for mesh entry points. DirectX 12 and Vulkan obtain the size from shader code, while the Metal implementation reads this descriptor field when creating its mesh pipeline, so the renderer supplies `1 x 1 x 1` explicitly.

The render sequence is explicit:

1. Transition `drawable` to `TextureLayout.ColorAttachment`.
2. Begin a render pass that clears and writes that drawable.
3. Bind the mesh shading pipeline.
4. Call `DispatchMesh(1, 1, 1)` to launch one mesh workgroup.
5. End the render pass.
6. Return control to `App`, which performs the final presentation transition, submission, wait, and presentation.

`DispatchMesh` acts on the currently bound `MeshShadingPipeline`, so `SetPipeline` must precede it and the dispatch must be inside the render pass.

## Synchronization and Lifetime

`CreateMeshShadingPipeline` consumes the shader objects while constructing its native pipeline, so the two local shader objects can be disposed at the end of the constructor. The renderer keeps the resulting `MeshShadingPipeline` for rendering and disposes it in `Dispose`. The shared `App.Run` scope disposes the renderer before the swap chain and graphics context, preserving parent-child lifetime order.

The drawable and command buffer remain owned by `App`; the renderer only records commands against them. Because this example has no persistent GPU resources besides the pipeline, resize does not require any renderer-side recreation.

## Next Steps

- Add a task stage to cull or select meshlets before mesh shading.
- Replace the fixed triangle with bindless meshlet data while preserving the same submission and presentation ownership.
- See [Mesh Shading](../../docs/features/mesh-shading.md) for the full pipeline surface and indirect dispatch path.
