# Hello Triangle

This tutorial replaces the clear-only renderer with the smallest complete graphics workload: one vertex buffer, two Slang entry points, one graphics pipeline, and one direct draw.

You will use the application shell from [Prerequisites](prerequisites.md). `App` supplies a command buffer and the current swap-chain drawable, then performs the final transition, submission, and presentation.

## Shader

Create `Assets/Shaders/HelloTriangle.slang`:

```slang
struct VSInput
{
    float3 Position : POSITION0;

    float4 Color : COLOR0;
};

struct FSInput
{
    float4 Position : SV_POSITION;

    float4 Color : COLOR0;
};

[shader("vertex")]
FSInput VSMain(VSInput input)
{
    FSInput output;
    output.Position = float4(input.Position, 1.0);
    output.Color = input.Color;

    return output;
}

[shader("fragment")]
float4 FSMain(FSInput input) : SV_TARGET
{
    return input.Color;
}
```

The vertex entry point receives attributes from the vertex buffer and writes clip-space position. The fragment entry point returns the interpolated color.

## Renderer

Create `Renderers/HelloTriangleRenderer.cs`:

```csharp
namespace ZenithTutorials.Renderers;

internal unsafe sealed class HelloTriangleRenderer : IRenderer
{
    private readonly Buffer vertexBuffer;
    private readonly GraphicsPipeline pipeline;

    public HelloTriangleRenderer()
    {
        Vertex[] vertices =
        [
            new(new(0.0f, 0.6f, 0.0f), new(1.0f, 0.2f, 0.15f, 1.0f)),
            new(new(0.6f, -0.5f, 0.0f), new(0.15f, 0.85f, 0.35f, 1.0f)),
            new(new(-0.6f, -0.5f, 0.0f), new(0.2f, 0.45f, 1.0f, 1.0f))
        ];

        vertexBuffer = App.Context.CreateBuffer(BufferDesc.Vertex((uint)(sizeof(Vertex) * vertices.Length)));

        fixed (Vertex* pointer = vertices)
        {
            vertexBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length)
            });
        }

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Color });

        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("HelloTriangle.slang"), "VSMain"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("HelloTriangle.slang"), "FSMain"));

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [inputLayout],
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
        commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
        commandBuffer.Draw(3, 1, 0, 0);

        commandBuffer.EndRenderPass();
    }

    public void Resize(uint width, uint height)
    {
    }

    public void Dispose()
    {
        pipeline.Dispose();
        vertexBuffer.Dispose();
    }
}

[StructLayout(LayoutKind.Sequential)]
file struct Vertex(Vector3 position, Vector4 color)
{
    public Vector3 Position = position;

    public Vector4 Color = color;
}
```

## Run

Update `Program.cs`:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

App.Run<HelloTriangleRenderer>();
```

Run the project:

```bash
dotnet run
```

![Hello Triangle](../../images/hello-triangle.png)

## How It Works

### Vertex Upload

`BufferDesc.Vertex` creates a GPU-only vertex buffer with transfer-destination usage. `Buffer.Upload` stages the array through `TransferQueue` and waits for that upload to finish. This one-time convenience path keeps resource creation simple; larger applications can batch several uploads on one transfer command buffer.

The `InputLayout` computes a 28-byte stride from `Float3` followed by `Float4`. Its order and semantics match `VSInput` exactly.

### Pipeline Compatibility

`AttachmentFormats` makes the pipeline compatible with the swap-chain color format and one sample per pixel. This tutorial has no depth attachment, so `DepthStencilFormat` remains `null` and depth testing is disabled.

Shaders are compiled for `App.Context.GraphicsApi`. The temporary `Shader` objects can be disposed after pipeline creation, matching the lifetime used by the repository's renderers.

### Explicit Frame Commands

The renderer receives a pooled `GraphicsQueue` command buffer from `App`. It transitions the drawable to `ColorAttachment`, begins a pass that clears the image, binds the pipeline and vertex buffer, then records:

```csharp
commandBuffer.Draw(3, 1, 0, 0);
```

The arguments are vertex count, instance count, first vertex, and first instance. After `EndRenderPass`, the shared application transitions the same drawable to `Present`, submits the command buffer, waits for completion, and presents it.

## Synchronization and Lifetime

The shared application waits for the frame submission before presenting, so renderer-owned resources are no longer in use when shutdown begins. The pipeline depends on the vertex and fragment shader code only during creation, so the constructor uses `using Shader`. The persistent pipeline is disposed before the vertex buffer when the renderer shuts down.

## Next Steps

Continue with [Textured Quad](textured-quad.md) to add indexed drawing and bindless texture sampling.
