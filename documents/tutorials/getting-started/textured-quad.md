# Textured Quad

Render `Assets/Textures/shoko.png` on a quad made from two indexed triangles. This tutorial adds an ImageSharp texture upload, a sampler, and bindless resource handles while keeping frame submission and presentation in the shared `App`.

## Shader

The prerequisites project already copies `Assets/**/*` to the output directory. Keep the image at `Assets/Textures/shoko.png`, then create `Assets/Shaders/TexturedQuad.slang`:

```slang
struct VSInput
{
    float3 Position : POSITION0;

    float2 TexCoord : TEXCOORD0;
};

struct FSInput
{
    float4 Position : SV_POSITION;

    float2 TexCoord : TEXCOORD0;
};

struct Constants
{
    DescriptorHandle<Texture2D> Texture;

    DescriptorHandle<SamplerState> Sampler;
};

uniform Constants constants;

[shader("vertex")]
FSInput VSMain(VSInput input)
{
    FSInput output;
    output.Position = float4(input.Position, 1.0);
    output.TexCoord = input.TexCoord;

    return output;
}

[shader("fragment")]
float4 FSMain(FSInput input) : SV_TARGET
{
    return (*constants.Texture).Sample(*constants.Sampler, input.TexCoord);
}
```

`DescriptorHandle<T>` is Slang's typed view of a Zenith.NET bindless handle. The C# constant structure below stores the matching `ResourceHandle` values for the sampled texture and sampler.

## Renderer

Create `Renderers/TexturedQuadRenderer.cs`:

```csharp
namespace ZenithTutorials.Renderers;

internal unsafe sealed class TexturedQuadRenderer : IRenderer
{
    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Texture texture;
    private readonly Sampler sampler;
    private readonly Buffer constantBuffer;
    private readonly GraphicsPipeline pipeline;

    public TexturedQuadRenderer()
    {
        Vertex[] vertices =
        [
            new() { Position = new(-0.5f, 0.5f, 0.0f), TexCoord = new(0.0f, 0.0f) },
            new() { Position = new(0.5f, 0.5f, 0.0f), TexCoord = new(1.0f, 0.0f) },
            new() { Position = new(0.5f, -0.5f, 0.0f), TexCoord = new(1.0f, 1.0f) },
            new() { Position = new(-0.5f, -0.5f, 0.0f), TexCoord = new(0.0f, 1.0f) }
        ];

        uint[] indices = [0, 1, 2, 0, 2, 3];

        vertexBuffer = App.Context.CreateBuffer(BufferDesc.Vertex((uint)(sizeof(Vertex) * vertices.Length)));

        fixed (Vertex* pointer = vertices)
        {
            vertexBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length)
            });
        }

        indexBuffer = App.Context.CreateBuffer(BufferDesc.Index((uint)(sizeof(uint) * indices.Length)));

        fixed (uint* pointer = indices)
        {
            indexBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(uint) * indices.Length)
            });
        }

        string texturePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Textures", "shoko.png");
        texture = App.Context.LoadTextureFromFile(texturePath, generateMipMaps: true);
        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        Constants constants = new()
        {
            Texture = texture.SampledHandle,
            Sampler = sampler.Handle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float2, Semantic = ElementSemantic.TexCoord });

        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("TexturedQuad.slang"), "VSMain"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("TexturedQuad.slang"), "FSMain"));

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [App.ColorFormat],
                DepthStencilFormat = null,
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
        for (uint mipLevel = 0; mipLevel < texture.Desc.MipLevels; mipLevel++)
        {
            commandBuffer.Transition(texture, new() { MipLevel = mipLevel }, TextureLayout.Sampled);
        }

        commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.Clear(drawable, new(0.04f, 0.055f, 0.075f, 1.0f))], null);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
        commandBuffer.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.DrawIndexed(6, 1, 0, 0, 0);

        commandBuffer.EndRenderPass();
    }

    public void Resize(uint width, uint height)
    {
    }

    public void Dispose()
    {
        pipeline.Dispose();
        constantBuffer.Dispose();
        sampler.Dispose();
        texture.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 20)]
file struct Vertex
{
    [FieldOffset(0)]
    public Vector3 Position;

    [FieldOffset(12)]
    public Vector2 TexCoord;
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
file struct Constants
{
    [FieldOffset(0)]
    public ResourceHandle Texture;

    [FieldOffset(8)]
    public ResourceHandle Sampler;
}
```

## Run

Replace `Program.cs` with:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

App.Run<TexturedQuadRenderer>();
```

Run the project:

```bash
dotnet run
```

![Textured Quad](../../images/textured-quad.png)

## How It Works

The four vertices are reused by six indices, producing two triangles. `LoadTextureFromFile` uses ImageSharp to decode the PNG and upload its mip levels. Each mip is transitioned to `Sampled` before the render pass.

The texture's `SampledHandle` and the sampler's `Handle` occupy the same two 8-byte slots as the Slang descriptors. `SetConstantBuffer` exposes that 16-byte structure to both shader stages. The fragment shader dereferences the typed handles and samples the texture.

## Synchronization and Lifetime

The renderer explicitly transitions the sampled texture and swap-chain drawable before use, clears the drawable, and records the indexed draw. The shared `App` performs the final drawable transition, submission wait, and presentation. That wait completes the frame before shutdown, after which the renderer disposes its pipeline, constant buffer, sampler, texture, and geometry buffers before the graphics context.

## Next Steps

Continue with [Spinning Cube](spinning-cube.md) to add Model-View-Projection transforms, animation, and a depth attachment.
