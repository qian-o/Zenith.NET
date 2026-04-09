# Ray Tracing

Zenith.NET supports hardware-accelerated ray tracing through a two-level acceleration structure hierarchy and `RayQuery` inline tracing in compute shaders.

> [!NOTE]
> Ray tracing requires `context.Capabilities.RayTracingSupported == true`.

## Acceleration Structures

### Hierarchy

| Level | Type | Contains |
|-------|------|----------|
| **BLAS** | `BottomLevelAccelerationStructure` | Triangle meshes or procedural AABBs |
| **TLAS** | `TopLevelAccelerationStructure` | References to BLAS instances with transforms |

### Building a BLAS

BLAS is built from geometry descriptions via a `CommandBuffer`:

**Triangle geometry:**

```csharp
BottomLevelAccelerationStructure blas = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
{
    Geometries =
    [
        new()
        {
            Type = RayTracingGeometryType.Triangles,
            Triangles = new()
            {
                VertexBuffer = vertexBuffer,
                VertexFormat = PixelFormat.R32G32B32Float,
                VertexCount = vertexCount,
                VertexStrideInBytes = 12,
                IndexBuffer = indexBuffer,
                IndexFormat = IndexFormat.UInt32,
                IndexCount = indexCount,
                Transform = Matrix4x4.Identity
            },
            Flags = RayTracingGeometryFlags.Opaque
        }
    ],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});
```

**Procedural AABB geometry:**

```csharp
BottomLevelAccelerationStructure blas = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
{
    Geometries =
    [
        new()
        {
            Type = RayTracingGeometryType.AABBs,
            AABBs = new()
            {
                Buffer = aabbBuffer,
                Count = aabbCount,
                StrideInBytes = 24  // 2 × Vector3 (min, max)
            },
            Flags = RayTracingGeometryFlags.Opaque
        }
    ],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});
```

Buffers used for BLAS input require `BufferUsageFlags.AccelerationStructure`.

### Building a TLAS

TLAS combines BLAS instances into a scene:

```csharp
TopLevelAccelerationStructure tlas = commandBuffer.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
{
    Instances =
    [
        new()
        {
            AccelerationStructure = meshBlas,
            ID = 0,
            Mask = 0xFF,
            Transform = Matrix4x4.Identity,
            Flags = RayTracingInstanceFlags.None
        }
    ],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});
```

### RayTracingInstance

| Field | Description |
|-------|-------------|
| `AccelerationStructure` | The BLAS to instance |
| `ID` | User-defined instance ID (readable in shaders) |
| `Mask` | Visibility mask for ray filtering |
| `Transform` | 3×4 transform matrix |
| `Flags` | Instance behavior flags |

### Updating a TLAS

Update an existing TLAS in-place (e.g., for animated transforms):

```csharp
commandBuffer.UpdateAccelerationStructure(tlas, newDesc);
```

### Build Flags

| Flag | Description |
|------|-------------|
| `PreferFastTrace` | Optimize for ray traversal speed |
| `PreferFastBuild` | Optimize for build speed |
| `AllowUpdate` | Allow in-place updates (required for `UpdateAccelerationStructure`) |

## RayQuery

Zenith.NET uses `RayQuery` in compute shaders (inline ray tracing) rather than a dedicated ray tracing pipeline:

```hlsl
RaytracingAccelerationStructure scene;

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
        if (query.CandidateType() == CANDIDATE_PROCEDURAL_PRIMITIVE)
        {
            // Custom intersection test for procedural geometry
            float t = IntersectSphere(query.CandidateObjectRayOrigin(),
                                       query.CandidateObjectRayDirection(), sphere);
            if (t > 0)
                query.CommitProceduralPrimitiveHit(t);
        }
    }

    if (query.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
    {
        float t = query.CommittedRayT();
        // Shade triangle hit
    }
}
```

## Resource Binding

Bind the TLAS to a resource table as `ResourceType.AccelerationStructure`:

```csharp
ResourceBinding[] bindings =
[
    new() { Type = ResourceType.AccelerationStructure, Count = 1 },
    new() { Type = ResourceType.TextureReadWrite, Count = 1 }
];

resourceTable.Write(0, tlas);
resourceTable.Write(1, outputTexture);
```
