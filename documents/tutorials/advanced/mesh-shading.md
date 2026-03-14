# Mesh Shading

In this tutorial, you'll learn how to use mesh shading with Zenith.NET. We'll render a simple cube using the mesh shading pipeline, demonstrating the modern GPU-driven geometry processing approach.

> [!NOTE]
> This tutorial requires a GPU with mesh shading support. Check `Context.Capabilities.MeshShadingSupported` before using mesh shading features.

## Overview

We'll create a `MeshShadingRenderer` class that:

- Defines vertex and meshlet data structures
- Creates structured buffers for vertices, indices, and meshlets
- Builds a mesh shading pipeline with mesh and pixel shaders
- Dispatches mesh shading workgroups to render geometry

## Key Concepts

### What is Mesh Shading?

Mesh shading replaces the traditional vertex processing pipeline (Input Assembler → Vertex Shader → optional tessellation/geometry) with a more flexible compute-like model:

| Traditional Pipeline | Mesh Shading Pipeline |
|---------------------|----------------------|
| Input Assembler | (removed) |
| Vertex Shader | (removed) |
| Hull/Domain Shader | (removed) |
| Geometry Shader | (removed) |
| - | Amplification Shader (optional) |
| - | Mesh Shader |
| Rasterizer | Rasterizer |
| Pixel Shader | Pixel Shader |

### Meshlet Architecture

The mesh shading pipeline works with **meshlets** - small chunks of geometry that can be processed independently:

```
Mesh
├── Meshlet 0 (up to 64-256 vertices, 64-256 primitives)
├── Meshlet 1
├── Meshlet 2
└── ...
```

Each meshlet contains:
- **VertexOffset**: Starting index in the vertex buffer
- **VertexCount**: Number of vertices in this meshlet
- **PrimitiveOffset**: Starting index in the index buffer
- **PrimitiveCount**: Number of triangles in this meshlet

### Pipeline Stages

| Shader Stage | Description |
|--------------|-------------|
| **Amplification** (optional) | Determines how many mesh shading workgroups to spawn (LOD, culling) |
| **Mesh** | Outputs vertices and primitives directly to the rasterizer |
| **Pixel** | Standard fragment shading |

## The Renderer Class

Create a new file `Renderers/MeshShadingRenderer.cs`:

```csharp
namespace ZenithTutorials.Renderers;

internal unsafe class MeshShadingRenderer : IRenderer
{
    private const uint MaxPrimitives = 126;

    private const string ShaderSource = """
        static const uint MaxVertices = 64;
        static const uint MaxPrimitives = 126;

        struct Vertex
        {
            private float4 PositionAndPadding;

            private float4 NormalAndPadding;

            float2 TexCoord;

            private float padding2;

            private float padding3;

            property float3 Position { get { return PositionAndPadding.xyz; } }

            property float3 Normal { get { return NormalAndPadding.xyz; } }
        };

        struct Meshlet
        {
            uint VertexOffset;

            uint VertexCount;

            uint PrimitiveOffset;

            uint PrimitiveCount;
        };

        struct Triangle
        {
            private uint4 IndicesAndPadding;

            property uint3 Indices { get { return IndicesAndPadding.xyz; } }
        };

        struct TransformConstants
        {
            float4x4 MVP;
        };

        struct VertexOutput
        {
            float4 Position : SV_Position;

            float3 Normal : NORMAL;

            float2 TexCoord : TEXCOORD0;
        };

        ConstantBuffer<TransformConstants> transform;
        StructuredBuffer<Vertex> vertices;
        StructuredBuffer<Triangle> indices;
        StructuredBuffer<Meshlet> meshlets;

        [shader("mesh")]
        [numthreads(MaxPrimitives, 1, 1)]
        [outputtopology("triangle")]
        void MSMain(in uint groupId : SV_GroupID,
                    in uint groupThreadId : SV_GroupThreadID,
                    OutputVertices<VertexOutput, MaxVertices> outVertices,
                    OutputIndices<uint3, MaxPrimitives> outIndices)
        {
            Meshlet meshlet = meshlets[groupId];

            SetMeshOutputCounts(meshlet.VertexCount, meshlet.PrimitiveCount);

            if (groupThreadId < meshlet.VertexCount)
            {
                Vertex vertex = vertices[meshlet.VertexOffset + groupThreadId];

                VertexOutput output;
                output.Position = mul(float4(vertex.Position, 1.0), transform.MVP);
                output.Normal = vertex.Normal;
                output.TexCoord = vertex.TexCoord;

                outVertices[groupThreadId] = output;
            }

            if (groupThreadId < meshlet.PrimitiveCount)
            {
                outIndices[groupThreadId] = indices[meshlet.PrimitiveOffset + groupThreadId].Indices;
            }
        }

        [shader("pixel")]
        float4 PSMain(VertexOutput input) : SV_Target
        {
            // Simple directional lighting
            float3 lightDir = normalize(float3(1.0, 1.0, -1.0));
            float ndotl = max(dot(normalize(input.Normal), lightDir), 0.0);

            // Base color from texture coordinates
            float3 baseColor = float3(input.TexCoord, 0.5);

            // Ambient + diffuse lighting
            float3 ambient = baseColor * 0.2;
            float3 diffuse = baseColor * ndotl * 0.8;

            return float4(ambient + diffuse, 1.0);
        }
        """;

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer meshletBuffer;
    private readonly Buffer constantBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly ResourceTable resourceTable;
    private readonly MeshShadingPipeline pipeline;

    private readonly uint meshletCount;
    private float rotationAngle;

    public MeshShadingRenderer()
    {
        if (!App.Context.Capabilities.MeshShadingSupported)
        {
            throw new NotSupportedException("Mesh shading is not supported on this device.");
        }

        Vertex[] cubeVertices =
        [
            // Front face
            new() { Position = new(-0.5f, -0.5f,  0.5f), Normal = new( 0,  0,  1), TexCoord = new(0, 1) },
            new() { Position = new( 0.5f, -0.5f,  0.5f), Normal = new( 0,  0,  1), TexCoord = new(1, 1) },
            new() { Position = new( 0.5f,  0.5f,  0.5f), Normal = new( 0,  0,  1), TexCoord = new(1, 0) },
            new() { Position = new(-0.5f,  0.5f,  0.5f), Normal = new( 0,  0,  1), TexCoord = new(0, 0) },

            // Back face
            new() { Position = new( 0.5f, -0.5f, -0.5f), Normal = new( 0,  0, -1), TexCoord = new(0, 1) },
            new() { Position = new(-0.5f, -0.5f, -0.5f), Normal = new( 0,  0, -1), TexCoord = new(1, 1) },
            new() { Position = new(-0.5f,  0.5f, -0.5f), Normal = new( 0,  0, -1), TexCoord = new(1, 0) },
            new() { Position = new( 0.5f,  0.5f, -0.5f), Normal = new( 0,  0, -1), TexCoord = new(0, 0) },

            // Left face
            new() { Position = new(-0.5f, -0.5f, -0.5f), Normal = new(-1,  0,  0), TexCoord = new(0, 1) },
            new() { Position = new(-0.5f, -0.5f,  0.5f), Normal = new(-1,  0,  0), TexCoord = new(1, 1) },
            new() { Position = new(-0.5f,  0.5f,  0.5f), Normal = new(-1,  0,  0), TexCoord = new(1, 0) },
            new() { Position = new(-0.5f,  0.5f, -0.5f), Normal = new(-1,  0,  0), TexCoord = new(0, 0) },

            // Right face
            new() { Position = new( 0.5f, -0.5f,  0.5f), Normal = new( 1,  0,  0), TexCoord = new(0, 1) },
            new() { Position = new( 0.5f, -0.5f, -0.5f), Normal = new( 1,  0,  0), TexCoord = new(1, 1) },
            new() { Position = new( 0.5f,  0.5f, -0.5f), Normal = new( 1,  0,  0), TexCoord = new(1, 0) },
            new() { Position = new( 0.5f,  0.5f,  0.5f), Normal = new( 1,  0,  0), TexCoord = new(0, 0) },

            // Top face
            new() { Position = new(-0.5f,  0.5f,  0.5f), Normal = new( 0,  1,  0), TexCoord = new(0, 1) },
            new() { Position = new( 0.5f,  0.5f,  0.5f), Normal = new( 0,  1,  0), TexCoord = new(1, 1) },
            new() { Position = new( 0.5f,  0.5f, -0.5f), Normal = new( 0,  1,  0), TexCoord = new(1, 0) },
            new() { Position = new(-0.5f,  0.5f, -0.5f), Normal = new( 0,  1,  0), TexCoord = new(0, 0) },

            // Bottom face
            new() { Position = new(-0.5f, -0.5f, -0.5f), Normal = new( 0, -1,  0), TexCoord = new(0, 1) },
            new() { Position = new( 0.5f, -0.5f, -0.5f), Normal = new( 0, -1,  0), TexCoord = new(1, 1) },
            new() { Position = new( 0.5f, -0.5f,  0.5f), Normal = new( 0, -1,  0), TexCoord = new(1, 0) },
            new() { Position = new(-0.5f, -0.5f,  0.5f), Normal = new( 0, -1,  0), TexCoord = new(0, 0) }
        ];

        Triangle[] cubeTriangles =
        [
            // Front face
            new() { I0 = 0, I1 = 1, I2 = 2 },
            new() { I0 = 0, I1 = 2, I2 = 3 },
            // Back face
            new() { I0 = 4, I1 = 5, I2 = 6 },
            new() { I0 = 4, I1 = 6, I2 = 7 },
            // Left face
            new() { I0 = 8, I1 = 9, I2 = 10 },
            new() { I0 = 8, I1 = 10, I2 = 11 },
            // Right face
            new() { I0 = 12, I1 = 13, I2 = 14 },
            new() { I0 = 12, I1 = 14, I2 = 15 },
            // Top face
            new() { I0 = 16, I1 = 17, I2 = 18 },
            new() { I0 = 16, I1 = 18, I2 = 19 },
            // Bottom face
            new() { I0 = 20, I1 = 21, I2 = 22 },
            new() { I0 = 20, I1 = 22, I2 = 23 }
        ];

        Meshlet[] meshlets =
        [
            new()
            {
                VertexOffset = 0,
                VertexCount = (uint)cubeVertices.Length,
                PrimitiveOffset = 0,
                PrimitiveCount = (uint)cubeTriangles.Length
            }
        ];
        meshletCount = (uint)meshlets.Length;

        // Create vertex buffer
        vertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vertex) * cubeVertices.Length),
            StrideInBytes = (uint)sizeof(Vertex),
            Flags = BufferUsageFlags.ShaderResource
        });
        vertexBuffer.Upload(cubeVertices, 0);

        // Create index buffer (Triangle struct per triangle)
        indexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Triangle) * cubeTriangles.Length),
            StrideInBytes = (uint)sizeof(Triangle),
            Flags = BufferUsageFlags.ShaderResource
        });
        indexBuffer.Upload(cubeTriangles, 0);

        meshletBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Meshlet) * meshlets.Length),
            StrideInBytes = (uint)sizeof(Meshlet),
            Flags = BufferUsageFlags.ShaderResource
        });
        meshletBuffer.Upload(meshlets, 0);

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(TransformConstants),
            StrideInBytes = (uint)sizeof(TransformConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = BindingHelper.Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Mesh },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Mesh },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Mesh },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Mesh }
            )
        });

        resourceTable = App.Context.CreateResourceTable(new()
        {
            Layout = resourceLayout,
            Resources = [constantBuffer, vertexBuffer, indexBuffer, meshletBuffer]
        });

        using Shader meshShader = App.Context.LoadShaderFromSource(ShaderSource, "MSMain", ShaderStageFlags.Mesh);
        using Shader pixelShader = App.Context.LoadShaderFromSource(ShaderSource, "PSMain", ShaderStageFlags.Pixel);

        pipeline = App.Context.CreateMeshShadingPipeline(new()
        {
            RenderStates = new()
            {
                RasterizerState = RasterizerStates.CullBack,
                DepthStencilState = DepthStencilStates.Default,
                BlendState = BlendStates.Opaque
            },
            Amplification = null,
            Mesh = meshShader,
            Pixel = pixelShader,
            ResourceLayout = resourceLayout,
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = App.SwapChain.FrameBuffer.Output,
            MeshThreadGroupSizeX = MaxPrimitives,
            MeshThreadGroupSizeY = 1,
            MeshThreadGroupSizeZ = 1
        });
    }

    public void Update(double deltaTime)
    {
        rotationAngle += (float)deltaTime;
    }

    public void Render()
    {
        Matrix4x4 model = Matrix4x4.CreateRotationY(rotationAngle) * Matrix4x4.CreateRotationX(rotationAngle * 0.5f);
        Matrix4x4 view = Matrix4x4.CreateLookAt(new(0, 0, 3), Vector3.Zero, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(45.0f), (float)App.Width / App.Height, 0.1f, 100.0f);

        constantBuffer.Upload([new TransformConstants() { MVP = model * view * projection }], 0);

        CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        commandBuffer.BeginRenderPass(App.SwapChain.FrameBuffer, new()
        {
            ColorValues = [new(0.1f, 0.1f, 0.1f, 1.0f)],
            Depth = 1.0f,
            Stencil = 0,
            Flags = ClearFlags.All
        }, resourceTable);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetResourceTable(resourceTable);
        commandBuffer.DispatchMesh(meshletCount, 1, 1);

        commandBuffer.EndRenderPass();

        commandBuffer.Submit(waitForCompletion: true);
    }

    public void Resize(uint width, uint height)
    {
    }

    public void Dispose()
    {
        pipeline.Dispose();
        resourceTable.Dispose();
        resourceLayout.Dispose();
        constantBuffer.Dispose();
        meshletBuffer.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }
}

/// <summary>
/// Vertex structure with position and normal.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 48)]
file struct Vertex
{
    [FieldOffset(0)]
    public Vector3 Position;

    [FieldOffset(16)]
    public Vector3 Normal;

    [FieldOffset(32)]
    public Vector2 TexCoord;
}

/// <summary>
/// Triangle indices for mesh shading.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 16)]
file struct Triangle
{
    [FieldOffset(0)]
    public uint I0;

    [FieldOffset(4)]
    public uint I1;

    [FieldOffset(8)]
    public uint I2;
}

/// <summary>
/// Meshlet structure defining a chunk of geometry.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 16)]
file struct Meshlet
{
    [FieldOffset(0)]
    public uint VertexOffset;

    [FieldOffset(4)]
    public uint VertexCount;

    [FieldOffset(8)]
    public uint PrimitiveOffset;

    [FieldOffset(12)]
    public uint PrimitiveCount;
}

/// <summary>
/// Transform constants for the mesh.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
file struct TransformConstants
{
    [FieldOffset(0)]
    public Matrix4x4 MVP;
}
```

## Running the Tutorial

Update your `Program.cs` to run the `MeshShadingRenderer`:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

App.Run<MeshShadingRenderer>();

App.Cleanup();
```

Run the application:

```bash
dotnet run
```

## Result

![mesh-shading](../../images/mesh-shading.png)

## Code Breakdown

### Checking Mesh Shading Support

```csharp
if (!App.Context.Capabilities.MeshShadingSupported)
{
    throw new NotSupportedException("Mesh shading is not supported on this device.");
}
```

Always check `Capabilities.MeshShadingSupported` before using mesh shading features.

### Meshlet Data Structure

```csharp
[StructLayout(LayoutKind.Explicit, Size = 16)]
file struct Meshlet
{
    [FieldOffset(0)]
    public uint VertexOffset;

    [FieldOffset(4)]
    public uint VertexCount;

    [FieldOffset(8)]
    public uint PrimitiveOffset;

    [FieldOffset(12)]
    public uint PrimitiveCount;
}
```

Each meshlet describes a chunk of geometry:
- **VertexOffset/Count**: Range of vertices in the vertex buffer
- **PrimitiveOffset/Count**: Range of triangles in the index buffer

### Mesh Shader Entry Point

```slang
[shader("mesh")]
[numthreads(MaxPrimitives, 1, 1)]
[outputtopology("triangle")]
void MSMain(in uint groupId : SV_GroupID,
            in uint groupThreadId : SV_GroupThreadID,
            OutputVertices<VertexOutput, MaxVertices> outVertices,
            OutputIndices<uint3, MaxPrimitives> outIndices)
```

Key attributes:

| Attribute | Description |
|-----------|-------------|
| `[shader("mesh")]` | Marks this as a mesh shader entry point |
| `[numthreads(X,Y,Z)]` | Thread group size (typically MaxPrimitives threads) |
| `[outputtopology("triangle")]` | Output primitive type |
| `OutputVertices<T, N>` | Output vertex array (max N vertices) |
| `OutputIndices<uint3, N>` | Output index array (max N triangles) |

### Setting Output Counts

```slang
SetMeshOutputCounts(meshlet.VertexCount, meshlet.PrimitiveCount);
```

This must be called once per workgroup to declare how many vertices and primitives will be output.

### Dispatching Mesh Shading

```csharp
commandBuffer.DispatchMesh(meshletCount, 1, 1);
```

Unlike traditional `Draw` calls, mesh shading uses `DispatchMesh(X, Y, Z)` to launch workgroups:
- Each workgroup processes one meshlet
- Total workgroups = `meshletCount × 1 × 1`

### Creating the Pipeline

```csharp
pipeline = App.Context.CreateMeshShadingPipeline(new()
{
    RenderStates = new() { ... },
    Amplification = null,
    Mesh = meshShader,
    Pixel = pixelShader,
    ResourceLayout = resourceLayout,
    PrimitiveTopology = PrimitiveTopology.TriangleList,
    Output = App.SwapChain.FrameBuffer.Output,
    MeshThreadGroupSizeX = MaxPrimitives,
    MeshThreadGroupSizeY = 1,
    MeshThreadGroupSizeZ = 1
});
```

The `MeshShadingPipelineDesc` requires:
- `Amplification` - The compiled amplification shader (optional)
- `Mesh` - The compiled mesh shader
- `Pixel` - The compiled pixel shader
- `ResourceLayout` - Resource bindings (same as graphics pipelines)
- `ObjectThreadGroupSizeX/Y/Z` - Must match `[numthreads()]` in the amplification shader (if used)
- `MeshThreadGroupSizeX/Y/Z` - Must match `[numthreads()]` in the mesh shader

## Amplification Shader (Optional)

For more advanced scenarios, you can add an amplification shader to dynamically control meshlet dispatch:

```slang
struct AmplificationPayload
{
    uint MeshletIndices[32];
};

[shader("amplification")]
[numthreads(32, 1, 1)]
void ASMain(in uint groupId : SV_GroupID,
            in uint groupThreadId : SV_GroupThreadID)
{
    // Frustum culling, LOD selection, etc.
    bool visible = /* culling logic */;

    if (visible)
    {
        AmplificationPayload payload;
        payload.MeshletIndices[groupThreadId] = groupId * 32 + groupThreadId;

        // Dispatch mesh shading workgroups
        DispatchMesh(visibleCount, 1, 1, payload);
    }
}
```

## Best Practices

1. **Meshlet Size**: Keep meshlets within hardware limits (typically 64-256 vertices, 64-126 primitives)
2. **Thread Utilization**: Size `numthreads` to match your maximum primitive count
3. **Early Out**: Check thread bounds before writing to output arrays
4. **Preprocessing**: Generate meshlets offline for complex models
5. **Culling**: Use amplification shaders for GPU-driven culling

## Next Steps

Congratulations! You've completed all Zenith.NET tutorials.

For a complete rendering example combining multiple techniques, check out the [SponzaScene](https://github.com/qian-o/Zenith.NET/tree/master/sources/Experiments/SponzaScene) sample which demonstrates a deferred renderer with ray traced global illumination.

## Source Code

> [!TIP]
> View the complete source code on GitHub: [MeshShadingRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs)
