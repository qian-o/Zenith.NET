# Ray Tracing

Ray tracing in Zenith.NET provides hardware-accelerated ray-scene intersection through `RayQuery` in any shader stage. You build acceleration structures on the GPU, bind them as resources, and trace rays directly within your shader code.

> [!NOTE]
> Ray tracing requires hardware support. Check `Context.Capabilities.RayTracingSupported` before using these features.

## Acceleration Structures

Ray tracing uses a two-level hierarchy to organize scene geometry:

### Bottom-Level Acceleration Structure (BLAS)

A BLAS contains the actual geometry data. Each BLAS supports one or more geometry entries of two types:

| Geometry Type | Description | Struct |
|---------------|-------------|--------|
| `Triangles` | Standard triangle meshes with vertex/index buffers | `RayTracingTriangles` |
| `AABBs` | Axis-aligned bounding boxes for procedural geometry | `RayTracingAABBs` |

**Triangle geometry:**

```csharp
new RayTracingGeometry
{
    Type = RayTracingGeometryType.Triangles,
    Triangles = new()
    {
        VertexBuffer = vertexBuffer,
        VertexFormat = PixelFormat.R32G32B32Float,
        VertexCount = vertexCount,
        VertexStrideInBytes = (uint)sizeof(Vector3),
        IndexBuffer = indexBuffer,
        IndexFormat = IndexFormat.UInt32,
        IndexCount = indexCount,
        Transform = Matrix4x4.Identity
    },
    Flags = RayTracingGeometryFlags.Opaque
}
```

**Procedural AABB geometry:**

```csharp
new RayTracingGeometry
{
    Type = RayTracingGeometryType.AABBs,
    AABBs = new()
    {
        Buffer = aabbBuffer,
        Count = aabbCount,
        StrideInBytes = (uint)(sizeof(Vector3) * 2)
    },
    Flags = RayTracingGeometryFlags.Opaque
}
```

### Top-Level Acceleration Structure (TLAS)

A TLAS contains instances that reference BLAS entries with per-instance transforms. Multiple instances can share the same BLAS with different transforms.

```
TLAS (scene)
├── Instance 0 → BLAS 0 (floor triangles)
├── Instance 1 → BLAS 1 (sphere AABBs)
├── Instance 2 → BLAS 0 (reused geometry, different transform)
└── ...
```

```csharp
new RayTracingInstance
{
    AccelerationStructure = blas,
    InstanceID = 0,
    InstanceMask = 0xFF,
    Transform = Matrix4x4.Identity,
    Flags = RayTracingInstanceFlags.None
}
```

> [!IMPORTANT]
> Instance transforms only support rotation and scale. Translation is **not supported** — use world-space coordinates in your geometry directly.

## Building Acceleration Structures

Acceleration structures are built via `CommandBuffer.BuildAccelerationStructure`:

```csharp
CommandBuffer cmd = context.Graphics.CommandBuffer();

BottomLevelAccelerationStructure blas = cmd.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
{
    Geometries = [/* geometry entries */],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});

TopLevelAccelerationStructure tlas = cmd.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
{
    Instances = [/* instance entries */],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});

cmd.Submit(waitForCompletion: true);
```

### Build Flags

| Flag | Description |
|------|-------------|
| `PreferFastTrace` | Optimize for ray traversal speed (larger memory) |
| `PreferFastBuild` | Optimize for build speed (slower traversal) |
| `AllowUpdate` | Allow updating the structure in place |
| `AllowCompaction` | Allow compaction to reduce memory |
| `MinimizeMemory` | Minimize memory at the cost of build time |
| `PerformUpdate` | Update an existing structure rather than building from scratch |

## Resource Binding

The TLAS is bound to a compute pipeline as an `AccelerationStructure` resource:

```csharp
ResourceLayout layout = context.CreateResourceLayout(new()
{
    Bindings = BindingHelper.Bindings
    (
        new() { Type = ResourceType.AccelerationStructure, Count = 1, StageFlags = ShaderStageFlags.Compute },
        new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute }
    )
});

ResourceTable table = context.CreateResourceTable(new()
{
    Layout = layout,
    Resources = [tlas, outputTexture]
});
```

## Shader Usage (RayQuery)

In your shader, declare a `RaytracingAccelerationStructure` and use `RayQuery` to trace rays:

```slang
RaytracingAccelerationStructure scene;
RWTexture2D<float4> output;

[numthreads(16, 16, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    RayDesc ray;
    ray.Origin = cameraPos;
    ray.Direction = rayDir;
    ray.TMin = 0.001;
    ray.TMax = 1000.0;

    RayQuery<RAY_FLAG_NONE> query;
    query.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, ray);

    while (query.Proceed())
    {
        if (query.CandidateType() == CANDIDATE_NON_OPAQUE_TRIANGLE)
            query.CommitNonOpaqueTriangleHit();
        else if (query.CandidateType() == CANDIDATE_PROCEDURAL_PRIMITIVE)
            query.CommitProceduralPrimitiveHit(t);
    }

    if (query.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
    {
        // Handle triangle hit
    }
    else if (query.CommittedStatus() == COMMITTED_PROCEDURAL_PRIMITIVE_HIT)
    {
        // Handle procedural hit
    }
}
```

### RayQuery API

| Element | Description |
|---------|-------------|
| `RayQuery<FLAGS>` | Declares a ray query with template ray flags |
| `TraceRayInline` | Initiates the ray traversal |
| `Proceed()` | Advances traversal; returns `true` while candidates remain |
| `CandidateType()` | Returns the type of the current candidate hit |
| `CommitNonOpaqueTriangleHit()` | Accepts a triangle hit |
| `CommitProceduralPrimitiveHit(t)` | Accepts a procedural hit at distance `t` |
| `CommittedStatus()` | Returns the final hit result after traversal |

### Common Ray Flags

| Flag | Description |
|------|-------------|
| `RAY_FLAG_NONE` | Default behavior |
| `RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH` | Stop at the first hit (useful for shadow rays) |
| `RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES` | Ignore AABB geometry |
| `RAY_FLAG_CULL_BACK_FACING_TRIANGLES` | Cull back-facing triangles |

## Instance Flags

| Flag | Description |
|------|-------------|
| `None` | Default behavior |
| `TriangleCullDisable` | Disable triangle culling for this instance |
| `TriangleFrontCounterClockwise` | Use counter-clockwise winding as front face |
| `ForceOpaque` | Treat all geometry as opaque |
| `ForceNoOpaque` | Treat all geometry as non-opaque |

## See Also

- [Compute Pipeline](compute-pipeline.md) — Ray tracing dispatches through compute pipelines
- [Ray Tracing Tutorial](../../tutorials/advanced/ray-tracing.md) — Step-by-step ray tracing example with shadows
