# Ray Tracing

Ray tracing combines BLAS/TLAS acceleration structures with inline Slang `RayQuery` operations.

## Capability Check

Ray tracing support is runtime-gated:

```csharp
if (!context.Capabilities.RayTracingSupported)
{
    return;
}
```

## Acceleration Structure Model

| Level | Type | Content |
|-------|------|---------|
| BLAS | `BottomLevelAccelerationStructure` | Triangle or AABB geometry |
| TLAS | `TopLevelAccelerationStructure` | Instances of BLAS with transform/flags |

`CommandBuffer` provides build and update entry points:

- `BuildAccelerationStructure(BottomLevelAccelerationStructureDesc)`
- `BuildAccelerationStructure(TopLevelAccelerationStructureDesc)`
- `UpdateAccelerationStructure(...)`

## BLAS Geometry

Use `RayTracingGeometry.Triangles` for indexed or non-indexed triangle geometry:

```csharp
BottomLevelAccelerationStructure triangleBlas = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
{
    Geometries =
    [
        RayTracingGeometry.Triangles(new()
        {
            VertexBuffer = vertexBuffer,
            VertexFormat = PixelFormat.R32G32B32Float,
            VertexCount = vertexCount,
            VertexStrideInBytes = vertexStrideInBytes,
            IndexBuffer = indexBuffer,
            IndexFormat = IndexFormat.UInt32,
            IndexCount = indexCount,
            Transform = Matrix4x4.Identity
        }, isOpaque: true)
    ],
    BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
});
```

Use `RayTracingGeometry.Aabbs` for procedural primitives. The shader accepts or rejects candidates and supplies the exact hit distance.

```csharp
BottomLevelAccelerationStructure aabbBlas = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
{
    Geometries =
    [
        RayTracingGeometry.Aabbs(new()
        {
            Buffer = aabbBuffer,
            Count = aabbCount,
            StrideInBytes = aabbStrideInBytes
        }, isOpaque: true)
    ],
    BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
});
```

## TLAS Build and Update

Build TLAS from `RayTracingInstance[]`:

```csharp
TopLevelAccelerationStructure tlas = commandBuffer.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
{
    Instances =
    [
        new()
        {
            AccelerationStructure = triangleBlas,
            InstanceId = 0,
            VisibilityMask = 0xFF,
            Transform = Matrix4x4.Identity,
            Flags = RayTracingInstanceFlags.None
        }
    ],
    BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
});
```

Update in place when previously created with `AllowUpdate`:

```csharp
commandBuffer.UpdateAccelerationStructure(tlas, new TopLevelAccelerationStructureDesc
{
    Instances = updatedInstances,
    BuildFlags = AccelerationStructureBuildFlags.AllowUpdate | AccelerationStructureBuildFlags.PreferFastTrace
});
```

## Build Flags

Use `AllowUpdate` for structures updated in place, and choose `PreferFastTrace`, `PreferFastBuild`, or `MinimizeMemory` according to the workload.

## Shader Access

Expose `tlas.Handle` through constant data and resolve it as `DescriptorHandle<RaytracingAccelerationStructure>` in Slang. See [Bindless Resources](../fundamentals/bindless-resources.md) for the handle mapping contract.

## Inline RayQuery in Slang

Inline ray tracing uses `RayQuery` with `TraceRayInline`:

```slang
RayDesc ray;
ray.Origin = origin;
ray.Direction = direction;
ray.TMin = 0.001;
ray.TMax = 100000.0;

RayQuery<RAY_FLAG_NONE> query;
query.TraceRayInline(pathTracing.Scene, RAY_FLAG_NONE, 0xFF, ray);

while (query.Proceed())
{
}

if (query.CommittedStatus() == COMMITTED_NOTHING)
{
    // Miss
}
else
{
    uint primitiveIndex = query.CommittedPrimitiveIndex();
    float2 barycentrics = query.CommittedTriangleBarycentrics();
}
```

For shadow rays, a common fast path is `RayQuery<RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH>`.

Build or update acceleration structures before tracing, and keep every referenced BLAS alive while its TLAS is in use. See [Synchronization](../fundamentals/synchronization.md) for same-queue ordering and cross-queue waits.
