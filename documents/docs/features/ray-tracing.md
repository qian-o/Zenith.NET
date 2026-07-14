# Ray Tracing

Zenith.NET Ray Tracing combines BLAS/TLAS acceleration structures with inline shader `RayQuery` operations.

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

## BLAS: Triangle and AABB Geometry

Triangle BLAS:

```csharp
BottomLevelAccelerationStructure triangleBlas = commandBuffer.BuildAccelerationStructure(new()
{
    Geometries =
    [
        new()
        {
            Type = RayTracingGeometryType.Triangle,
            TriangleGeometry = new()
            {
                VertexBuffer = vertexBuffer,
                VertexFormat = PixelFormat.R32G32B32Float,
                VertexCount = vertexCount,
                VertexStrideInBytes = vertexStrideInBytes,
                VertexOffsetInBytes = 0,
                IndexBuffer = indexBuffer,
                IndexFormat = IndexFormat.UInt32,
                IndexCount = indexCount,
                IndexOffsetInBytes = 0,
                Transform = Matrix4x4.Identity
            },
            IsOpaque = true
        }
    ],
    BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
});
```

AABB BLAS:

```csharp
BottomLevelAccelerationStructure aabbBlas = commandBuffer.BuildAccelerationStructure(new()
{
    Geometries =
    [
        new()
        {
            Type = RayTracingGeometryType.Aabb,
            AabbGeometry = new()
            {
                Buffer = aabbBuffer,
                Count = aabbCount,
                StrideInBytes = 24,
                OffsetInBytes = 0
            },
            IsOpaque = true
        }
    ],
    BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
});
```

You can also use helpers:

- `RayTracingGeometry.Triangles(...)`
- `RayTracingGeometry.Aabbs(...)`

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

`AccelerationStructureBuildFlags`:

- `None`
- `AllowUpdate`
- `AllowCompaction`
- `PreferFastTrace`
- `PreferFastBuild`
- `MinimizeMemory`

Pick flags based on scene behavior (static vs dynamic) and memory/performance goals.

`AllowCompaction` maps to the native build hint where the Graphics API supports it, but the current RHI does not expose an acceleration-structure compaction command. Do not select it unless the application integrates the required native operation separately.

## Bindless TLAS Handles

Bind TLAS through a `ResourceHandle` in your constant struct:

```csharp
[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct PathTracingConstants
{
    [FieldOffset(0)]
    public ResourceHandle Scene;

    [FieldOffset(8)]
    public ResourceHandle OutputTexture;
}

PathTracingConstants constants = new()
{
    Scene = tlas.Handle,
    OutputTexture = outputTexture.StorageHandle
};
```

Shader side:

```slang
struct PathTracingConstants
{
    DescriptorHandle<RaytracingAccelerationStructure> Scene;
    DescriptorHandle<RWTexture2D<float4>> OutputTexture;
};

uniform PathTracingConstants pathTracing;
```

See [Bindless Resources](../concepts/resource-binding.md) for handle lifetime and mapping rules.

## Inline RayQuery in Slang

Inline ray tracing uses `RayQuery` with `TraceRayInline`:

```slang
RayDesc ray;
ray.Origin = origin;
ray.Direction = direction;
ray.TMin = 0.001;
ray.TMax = 100000.0;

RayQuery<RAY_FLAG_NONE> query;
query.TraceRayInline(*pathTracing.Scene, RAY_FLAG_NONE, 0xFF, ray);

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

## Synchronization and Lifetime

Ray tracing resources follow normal explicit synchronization and ownership rules:

- Build/update BLAS or TLAS on a command buffer, then synchronize before use on another queue with `TimelineValue` waits.
- Keep BLAS/TLAS alive until all submissions that may access their handles are complete.
- Rebuild or update TLAS when instance transforms or membership changes.

Example queue dependency:

```csharp
TimelineValue built = buildCommands.Submit();
TimelineValue traced = traceCommands.Submit(built);
traced.Wait();
```

See [Synchronization and Barriers](../concepts/synchronization.md) for queue and barrier guidance.
