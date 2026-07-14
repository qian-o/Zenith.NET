# Spinning Cube

Render a continuously rotating, indexed 3D cube with per-vertex colors. This tutorial adds a Model-View-Projection constant buffer, back-face culling, depth testing, and a depth texture that follows the drawable size.

## Shader

Create `Assets/Shaders/SpinningCube.slang`:

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

struct Constants
{
    float4x4 Model;

    float4x4 View;

    float4x4 Projection;
};

uniform Constants constants;

[shader("vertex")]
FSInput VSMain(VSInput input)
{
    float4x4 modelView = mul(constants.Model, constants.View);
    float4x4 mvp = mul(modelView, constants.Projection);

    FSInput output;
    output.Position = mul(float4(input.Position, 1.0), mvp);
    output.Color = input.Color;

    return output;
}

[shader("fragment")]
float4 FSMain(FSInput input) : SV_TARGET
{
    return input.Color;
}
```

The shader receives three 4x4 matrices through one `uniform` constant block. Its row-vector multiplication order matches `System.Numerics.Matrix4x4` and the current CornellBox shaders.

## Renderer

Create `Renderers/SpinningCubeRenderer.cs`:

```csharp
namespace ZenithTutorials.Renderers;

internal unsafe sealed class SpinningCubeRenderer : IRenderer
{
    private const PixelFormat DepthFormat = PixelFormat.D32FloatS8UInt;

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer constantBuffer;
    private readonly GraphicsPipeline pipeline;

    private Texture depthTexture;
    private float rotationAngle;

    public SpinningCubeRenderer()
    {
        Vertex[] vertices =
        [
            new() { Position = new(-0.5f, -0.5f, 0.5f), Color = new(1.0f, 0.0f, 0.0f, 1.0f) },
            new() { Position = new(0.5f, -0.5f, 0.5f), Color = new(0.0f, 1.0f, 0.0f, 1.0f) },
            new() { Position = new(0.5f, 0.5f, 0.5f), Color = new(0.0f, 0.0f, 1.0f, 1.0f) },
            new() { Position = new(-0.5f, 0.5f, 0.5f), Color = new(1.0f, 1.0f, 0.0f, 1.0f) },
            new() { Position = new(-0.5f, -0.5f, -0.5f), Color = new(1.0f, 0.0f, 1.0f, 1.0f) },
            new() { Position = new(0.5f, -0.5f, -0.5f), Color = new(0.0f, 1.0f, 1.0f, 1.0f) },
            new() { Position = new(0.5f, 0.5f, -0.5f), Color = new(1.0f, 1.0f, 1.0f, 1.0f) },
            new() { Position = new(-0.5f, 0.5f, -0.5f), Color = new(0.5f, 0.5f, 0.5f, 1.0f) }
        ];

        uint[] indices =
        [
            0, 1, 2, 0, 2, 3,
            5, 4, 7, 5, 7, 6,
            4, 0, 3, 4, 3, 7,
            1, 5, 6, 1, 6, 2,
            3, 2, 6, 3, 6, 7,
            4, 5, 1, 4, 1, 0
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

        indexBuffer = App.Context.CreateBuffer(BufferDesc.Index((uint)(sizeof(uint) * indices.Length)));

        fixed (uint* pointer = indices)
        {
            indexBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(uint) * indices.Length)
            });
        }

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        depthTexture = CreateDepthTexture(App.Width, App.Height);

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Color });

        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("SpinningCube.slang"), "VSMain"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("SpinningCube.slang"), "FSMain"));

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [App.ColorFormat],
                DepthStencilFormat = DepthFormat,
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullBack(),
                DepthStencil = DepthStencilState.DepthReadWrite(),
                Blend = BlendState.Opaque()
            }
        });

        Update(0.0);
    }

    public void Update(double deltaTime)
    {
        rotationAngle += (float)deltaTime;

        Matrix4x4 model = Matrix4x4.CreateRotationY(rotationAngle) * Matrix4x4.CreateRotationX(rotationAngle * 0.5f);
        Matrix4x4 view = Matrix4x4.CreateLookAt(new(0.0f, 0.0f, 3.0f), Vector3.Zero, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(45.0f), (float)App.Width / App.Height, 0.1f, 100.0f);

        Constants constants = new()
        {
            Model = model,
            View = view,
            Projection = projection
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });
    }

    public void Render(CommandBuffer commandBuffer, Texture drawable)
    {
        commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment);
        commandBuffer.Transition(depthTexture, default, TextureLayout.DepthStencilAttachment);

        commandBuffer.BeginRenderPass([ColorAttachment.Clear(drawable, new(0.04f, 0.055f, 0.075f, 1.0f))], DepthStencilAttachment.Clear(depthTexture, 1.0f, 0));

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
        commandBuffer.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.DrawIndexed(36, 1, 0, 0, 0);

        commandBuffer.EndRenderPass();
    }

    public void Resize(uint width, uint height)
    {
        depthTexture.Dispose();
        depthTexture = CreateDepthTexture(width, height);
    }

    public void Dispose()
    {
        pipeline.Dispose();
        depthTexture.Dispose();
        constantBuffer.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }

    private static Texture CreateDepthTexture(uint width, uint height)
    {
        return App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = DepthFormat,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.DepthStencilAttachment
        });
    }
}

[StructLayout(LayoutKind.Explicit, Size = 28)]
file struct Vertex
{
    [FieldOffset(0)]
    public Vector3 Position;

    [FieldOffset(12)]
    public Vector4 Color;
}

[StructLayout(LayoutKind.Explicit, Size = 192)]
file struct Constants
{
    [FieldOffset(0)]
    public Matrix4x4 Model;

    [FieldOffset(64)]
    public Matrix4x4 View;

    [FieldOffset(128)]
    public Matrix4x4 Projection;
}
```

## Run

Replace `Program.cs` with:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

App.Run<SpinningCubeRenderer>();
```

Run the project:

```bash
dotnet run
```

![Spinning Cube](../../images/spinning-cube.png)

## How It Works

`Update` accumulates elapsed time, composes the model rotation, builds a camera view, and recalculates the perspective projection from the current drawable aspect ratio. The three matrices occupy 192 bytes and are uploaded to the CPU-writable constant buffer before `SetConstantBuffer` binds it.

The pipeline enables back-face culling and read-write depth testing. Before each pass, the renderer transitions the drawable to `ColorAttachment` and its depth texture to `DepthStencilAttachment`; both are cleared when the pass begins. The depth test keeps only the closest cube surfaces.

## Synchronization and Lifetime

`Resize` disposes the old depth texture and creates one matching the new width and height. This is safe with the shared synchronous frame loop, and `Dispose` releases the final depth texture with the other renderer-owned resources. The shared `App` still owns final drawable presentation.

## Next Steps

Continue with [Compute Shader](../intermediate/compute-shader.md) to move from graphics work to general GPU dispatch.
