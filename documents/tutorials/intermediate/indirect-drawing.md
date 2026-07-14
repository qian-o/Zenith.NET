# Indirect Drawing

Indirect drawing lets the GPU provide draw parameters instead of passing them directly from the CPU. In this tutorial, a compute shader generates both a grid of animated instances and one `IndirectDrawIndexedArgs` command. The same command buffer then places an explicit `Barrier` before consuming those buffers with `DrawIndexedIndirect`.

You will use:

- A GPU-only buffer with `StorageReadWrite | Indirect` usages
- The exact `IndirectDrawIndexedArgs` field layout and stride
- Bindless read/write and read-only views of an instance buffer
- `Barrier(ComputeShading, VertexShading)` as a first-class RHI dependency
- Compute and graphics pipelines in one `GraphicsQueue` command buffer

Start from the [shared application](../getting-started/prerequisites.md). Add the shader and renderer below, then select the renderer in `Program.cs`.

## Shader

Create `Assets/Shaders/IndirectDrawing.slang`:

```slang
struct InstanceData
{
    float2 Offset;

    float Scale;

    float Angle;

    float4 Color;
};

struct IndirectDrawIndexedArgs
{
    uint IndexCount;

    uint InstanceCount;

    uint FirstIndex;

    int VertexOffset;

    uint FirstInstance;
};

struct Constants
{
    float Time;

    uint InstanceCount;

    uint GridWidth;

    uint IndexCount;

    DescriptorHandle<RWStructuredBuffer<InstanceData>> WritableInstances;

    DescriptorHandle<RWStructuredBuffer<IndirectDrawIndexedArgs>> DrawArguments;

    DescriptorHandle<StructuredBuffer<InstanceData>> Instances;
};

uniform Constants constants;

[shader("compute")]
[numthreads(64, 1, 1)]
void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint instanceIndex = dispatchThreadID.x;

    if (instanceIndex == 0)
    {
        IndirectDrawIndexedArgs arguments;
        arguments.IndexCount = constants.IndexCount;
        arguments.InstanceCount = constants.InstanceCount;
        arguments.FirstIndex = 0;
        arguments.VertexOffset = 0;
        arguments.FirstInstance = 0;

        (*constants.DrawArguments)[0] = arguments;
    }

    if (instanceIndex >= constants.InstanceCount)
    {
        return;
    }

    uint column = instanceIndex % constants.GridWidth;
    uint row = instanceIndex / constants.GridWidth;
    float halfGrid = (float(constants.GridWidth) - 1.0) * 0.5;
    float spacing = 1.6 / max(float(constants.GridWidth) - 1.0, 1.0);

    InstanceData instance;
    instance.Offset = (float2(column, row) - halfGrid) * spacing;
    instance.Scale = 0.13 + 0.02 * sin(constants.Time * 1.7 + float(instanceIndex));
    instance.Angle = constants.Time * (0.7 + float(instanceIndex) * 0.03);
    instance.Color = float4(0.5 + 0.5 * cos(float(instanceIndex) * 0.37 + float3(0.0, 2.1, 4.2)), 1.0);

    (*constants.WritableInstances)[instanceIndex] = instance;
}

struct VSInput
{
    float2 Position : POSITION0;

    uint InstanceID : SV_InstanceID;
};

struct VSOutput
{
    float4 Position : SV_POSITION;

    float4 Color : COLOR0;
};

[shader("vertex")]
VSOutput VSMain(VSInput input)
{
    InstanceData instance = (*constants.Instances)[input.InstanceID];
    float sine = sin(instance.Angle);
    float cosine = cos(instance.Angle);
    float2 rotated = float2(input.Position.x * cosine - input.Position.y * sine, input.Position.x * sine + input.Position.y * cosine);

    VSOutput output;
    output.Position = float4(instance.Offset + rotated * instance.Scale, 0.0, 1.0);
    output.Color = instance.Color;

    return output;
}

[shader("fragment")]
float4 FSMain(VSOutput input) : SV_TARGET
{
    return input.Color;
}
```

The Slang argument structure deliberately mirrors the RHI type field for field. All five members are 32-bit values, so its stride is 20 bytes. `VertexOffset` is signed; the other fields are unsigned.

## Renderer

Create `Renderers/IndirectDrawingRenderer.cs`:

```csharp
namespace ZenithTutorials.Renderers;

internal unsafe sealed class IndirectDrawingRenderer : IRenderer
{
    private const uint ThreadGroupSize = 64;
    private const uint InstanceCount = 25;
    private const uint GridWidth = 5;
    private const uint IndexCount = 6;

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer instanceBuffer;
    private readonly Buffer indirectBuffer;
    private readonly Buffer constantBuffer;
    private readonly ComputePipeline computePipeline;
    private readonly GraphicsPipeline graphicsPipeline;

    private float elapsedTime;

    public IndirectDrawingRenderer()
    {
        Vertex[] vertices =
        [
            new(new(-1.0f, -1.0f)),
            new(new( 1.0f, -1.0f)),
            new(new( 1.0f,  1.0f)),
            new(new(-1.0f,  1.0f))
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

        instanceBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(InstanceData) * InstanceCount,
            StrideInBytes = (uint)sizeof(InstanceData),
            Usages = BufferUsages.StorageReadWrite | BufferUsages.StorageReadOnly,
            Residency = MemoryResidency.GpuOnly
        });

        indirectBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(IndirectDrawIndexedArgs),
            StrideInBytes = (uint)sizeof(IndirectDrawIndexedArgs),
            Usages = BufferUsages.StorageReadWrite | BufferUsages.Indirect,
            Residency = MemoryResidency.GpuOnly
        });

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(IndirectConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        using Shader computeShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("IndirectDrawing.slang"), "CSMain"));
        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("IndirectDrawing.slang"), "VSMain"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("IndirectDrawing.slang"), "FSMain"));

        computePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = computeShader });

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float2, Semantic = ElementSemantic.Position });

        graphicsPipeline = App.Context.CreateGraphicsPipeline(new()
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

        UploadConstants();
    }

    public void Update(double deltaTime)
    {
        elapsedTime += (float)deltaTime;
        UploadConstants();
    }

    public void Render(CommandBuffer commandBuffer, Texture drawable)
    {
        commandBuffer.SetPipeline(computePipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);

        uint groupCount = (InstanceCount + ThreadGroupSize - 1) / ThreadGroupSize;
        commandBuffer.Dispatch(groupCount, 1, 1);

        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.VertexShading);

        commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.Clear(drawable, new(0.025f, 0.03f, 0.045f, 1.0f))], null);

        commandBuffer.SetPipeline(graphicsPipeline);
        commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
        commandBuffer.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.DrawIndexedIndirect(indirectBuffer, 0, 1);

        commandBuffer.EndRenderPass();
    }

    public void Resize(uint width, uint height)
    {
    }

    public void Dispose()
    {
        graphicsPipeline.Dispose();
        computePipeline.Dispose();
        constantBuffer.Dispose();
        indirectBuffer.Dispose();
        instanceBuffer.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }

    private void UploadConstants()
    {
        IndirectConstants constants = new()
        {
            Time = elapsedTime,
            InstanceCount = InstanceCount,
            GridWidth = GridWidth,
            IndexCount = IndexCount,
            WritableInstances = instanceBuffer.StorageReadWriteHandle,
            DrawArguments = indirectBuffer.StorageReadWriteHandle,
            Instances = instanceBuffer.StorageReadOnlyHandle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(IndirectConstants)
        });
    }
}

[StructLayout(LayoutKind.Sequential)]
file struct Vertex(Vector2 position)
{
    public Vector2 Position = position;
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct InstanceData
{
    [FieldOffset(0)]
    public Vector2 Offset;

    [FieldOffset(8)]
    public float Scale;

    [FieldOffset(12)]
    public float Angle;

    [FieldOffset(16)]
    public Vector4 Color;
}

[StructLayout(LayoutKind.Explicit, Size = 256)]
file struct IndirectConstants
{
    [FieldOffset(0)]
    public float Time;

    [FieldOffset(4)]
    public uint InstanceCount;

    [FieldOffset(8)]
    public uint GridWidth;

    [FieldOffset(12)]
    public uint IndexCount;

    [FieldOffset(16)]
    public ResourceHandle WritableInstances;

    [FieldOffset(24)]
    public ResourceHandle DrawArguments;

    [FieldOffset(32)]
    public ResourceHandle Instances;
}
```

## Run

Replace `Program.cs` with:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

App.Run<IndirectDrawingRenderer>();
```

Run the project:

```bash
dotnet run
```

## How It Works

### Argument Layout and Buffer Roles

Zenith.NET defines `IndirectDrawIndexedArgs` in this exact order:

| Field | Type | Value in this tutorial |
|-------|------|------------------------|
| `IndexCount` | `uint` | `6` |
| `InstanceCount` | `uint` | `25` |
| `FirstIndex` | `uint` | `0` |
| `VertexOffset` | `int` | `0` |
| `FirstInstance` | `uint` | `0` |

The structure is 20 bytes. `StrideInBytes = (uint)sizeof(IndirectDrawIndexedArgs)` therefore matches the stride used by `DrawIndexedIndirect` on every supported Graphics API. The compute shader writes one element, and `DrawIndexedIndirect(indirectBuffer, 0, 1)` consumes one command at byte offset zero.

The indirect buffer is `GpuOnly` and combines `StorageReadWrite` with `Indirect`: compute needs a writable descriptor, while the draw command processor needs indirect-argument access. The instance buffer combines `StorageReadWrite` and `StorageReadOnly`, exposing separate bindless handles for compute writes and vertex-shader reads. Neither buffer needs CPU mapping or a transfer usage because compute initializes both every frame.

The static vertex and index buffers are also `GpuOnly`. Their `BufferDesc.Vertex` and `BufferDesc.Index` helpers include `TransferDst`, so the constructor's `Upload` calls stage the immutable geometry through the transfer queue and wait before returning. Only the small constants buffer is `CpuWriteOnly` and updated by the CPU.

## Synchronization and Lifetime

`Dispatch` writes both GPU-only buffers, but buffers do not have texture layouts to transition. The dependency is expressed directly:

```csharp
commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.VertexShading);
```

Within Zenith.NET, the `VertexShading` destination covers the indexed-draw inputs, indirect-command read, and vertex shader read used here. The barrier makes the compute writes available before `DrawIndexedIndirect` reads the arguments and the vertex shader reads the instances. It is not a CPU wait and it does not split the command buffer.

Both pipelines run on the `GraphicsQueue` command buffer supplied by `App`, preserving their order in one submission. The shared shell performs the final transition of the drawable to `Present`, calls `Submit().Wait()`, and presents it. Because that wait completes the submitted work, all renderer-owned buffers and pipelines can be disposed before the `GraphicsContext` at shutdown.

## Next Steps

Continue with [Ray Tracing](../advanced/ray-tracing.md) to build and query acceleration structures through the same explicit RHI.
