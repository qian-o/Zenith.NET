# Mesh Shading

The mesh shading pipeline replaces the traditional vertex/geometry pipeline with programmable mesh and amplification shaders, enabling GPU-driven geometry processing.

> [!NOTE]
> Mesh shading requires `context.Capabilities.MeshShadingSupported == true`.

## Pipeline Stages

| Stage | Shader Flag | Purpose |
|-------|-------------|---------|
| **Amplification** | `ShaderStageFlags.Amplification` | Optional. Controls how many mesh groups to dispatch (culling, LOD) |
| **Mesh** | `ShaderStageFlags.Mesh` | Required. Outputs vertices and triangles per thread group |
| **Pixel** | `ShaderStageFlags.Pixel` | Standard fragment shading |

## Mesh Shading Pipeline

```csharp
MeshShadingPipeline pipeline = context.CreateMeshShadingPipeline(new MeshShadingPipelineDesc
{
    RenderStates = new()
    {
        RasterizerState = RasterizerStates.CullBack,
        DepthStencilState = DepthStencilStates.Default,
        BlendState = BlendStates.Opaque
    },
    Amplification = ampShader,  // optional
    Mesh = meshShader,
    Pixel = pixelShader,
    ResourceBindings = resourceBindings,
    PrimitiveTopology = PrimitiveTopology.TriangleList,
    Output = frameBuffer.Output,
    AmplificationThreadGroupSizeX = 32,
    AmplificationThreadGroupSizeY = 1,
    AmplificationThreadGroupSizeZ = 1,
    MeshThreadGroupSizeX = 128,
    MeshThreadGroupSizeY = 1,
    MeshThreadGroupSizeZ = 1
});
```

### MeshShadingPipelineDesc

| Field | Type | Description |
|-------|------|-------------|
| `RenderStates` | `RenderStates` | Rasterizer, depth/stencil, and blend configuration |
| `Amplification` | `Shader?` | Optional amplification shader |
| `Mesh` | `Shader` | Mesh shader (required) |
| `Pixel` | `Shader` | Pixel shader |
| `ResourceBindings` | `ResourceBinding[]` | Resource binding declarations |
| `PrimitiveTopology` | `PrimitiveTopology` | Primitive assembly mode |
| `Output` | `Output` | Render target format description |
| `AmplificationThreadGroupSize*` | `uint` | Thread group size for the amplification stage |
| `MeshThreadGroupSize*` | `uint` | Thread group size for the mesh stage |

## Amplification Shader

The amplification shader runs per-group and decides how many mesh groups to spawn:

```hlsl
struct Payload
{
    uint InstanceIndices[32];
};

groupshared Payload s_payload;
groupshared uint s_visibleCount;

[shader("amplification")]
[numthreads(32, 1, 1)]
void ASMain(uint groupID : SV_GroupID, uint threadID : SV_GroupThreadID)
{
    bool visible = !IsCulled(instancePosition);

    if (threadID == 0)
        s_visibleCount = 0;

    GroupMemoryBarrierWithGroupSync();

    if (visible)
    {
        uint offset;
        InterlockedAdd(s_visibleCount, 1, offset);
        s_payload.InstanceIndices[offset] = groupID * 32 + threadID;
    }

    GroupMemoryBarrierWithGroupSync();

    DispatchMesh(s_visibleCount, 1, 1, s_payload);
}
```

## Mesh Shader

The mesh shader outputs vertices and triangle indices directly:

```hlsl
struct VertexOutput
{
    float4 Position : SV_POSITION;
    float3 Normal : WORLDNORMAL;
    float3 Color : COLOR;
};

[shader("mesh")]
[numthreads(128, 1, 1)]
[outputtopology("triangle")]
void MSMain(uint groupID : SV_GroupID, uint threadID : SV_GroupThreadID,
            in payload Payload meshPayload,
            OutputVertices<VertexOutput, 64> outVertices,
            OutputIndices<uint3, 128> outIndices)
{
    SetMeshOutputCounts(vertexCount, triangleCount);

    if (threadID < vertexCount)
        outVertices[threadID] = /* compute vertex */;

    if (threadID < triangleCount)
        outIndices[threadID] = /* triangle indices */;
}
```

Geometry data is typically stored in `StructuredBuffer` resources rather than traditional vertex/index buffers.

## Dispatching

```csharp
commandBuffer.BeginRenderPass(frameBuffer, clearValue, resourceTable);
commandBuffer.SetPipeline(pipeline);
commandBuffer.PushResourceTable(resourceTable);
commandBuffer.DispatchMesh(groupCountX, groupCountY, groupCountZ);
commandBuffer.EndRenderPass();
```

### Indirect Dispatch

```csharp
commandBuffer.DispatchMeshIndirect(indirectBuffer, offsetInBytes, dispatchCount);
```

## Common Patterns

### Frustum Culling

The amplification shader is commonly used for GPU-driven frustum culling:

1. Each thread tests one instance against camera frustum planes
2. Visible instances are compacted into a `groupshared` payload using atomics
3. `DispatchMesh` spawns only the visible count

### Meshlet Rendering

Mesh shaders naturally map to meshlet-based rendering:
- Pre-split geometry into meshlets (groups of ≤ 64 vertices, ≤ 128 triangles)
- Each mesh shader group processes one meshlet
- Amplification shader selects visible meshlets
