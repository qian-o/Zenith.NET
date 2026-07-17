# Ray Tracing

Zenith.NET Ray Tracing uses bottom-level and top-level acceleration structures with inline Slang `RayQuery` operations.

## Check Support

Check the capability before creating Ray Tracing resources:

```csharp
if (!context.Capabilities.RayTracingSupported)
{
    return;
}
```

## Build a BLAS

A bottom-level acceleration structure (BLAS) contains triangle or axis-aligned bounding-box geometry. Record its build in a command buffer:

```csharp
BottomLevelAccelerationStructure triangleBlas = commandBuffer.BuildAccelerationStructure(new()
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

Use `RayTracingGeometry.Aabbs` instead when the geometry is represented by bounding boxes:

```csharp
BottomLevelAccelerationStructure aabbBlas = commandBuffer.BuildAccelerationStructure(new()
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

## Build a TLAS

The top-level acceleration structure (TLAS) contains instances of existing BLAS objects:

```csharp
TopLevelAccelerationStructure tlas = commandBuffer.BuildAccelerationStructure(new()
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

Submit and complete the build before a later submission traces against the TLAS:

```csharp
commandBuffer.Submit().Wait();
```

Keep every referenced BLAS alive while the TLAS is in use.

## Update an Acceleration Structure

Include `AllowUpdate` when first building a structure that will be updated in place. Record later updates with the same flag:

```csharp
commandBuffer.UpdateAccelerationStructure(tlas, new()
{
    Instances = updatedInstances,
    BuildFlags = AccelerationStructureBuildFlags.AllowUpdate | AccelerationStructureBuildFlags.PreferFastTrace
});
```

Choose `PreferFastTrace`, `PreferFastBuild`, or `MinimizeMemory` according to how the structure is used.

## Trace Rays in Slang

Store `tlas.Handle` in constant data and declare it as a typed descriptor:

```slang
DescriptorHandle<RaytracingAccelerationStructure> Scene;
```

For triangle geometry, initialize a `RayQuery`, trace it, and inspect the committed result:

```slang
RayDesc ray;
ray.Origin = origin;
ray.Direction = direction;
ray.TMin = 0.001;
ray.TMax = 100000.0;

RayQuery<RAY_FLAG_NONE> query;
query.TraceRayInline(constants.Scene, RAY_FLAG_NONE, 0xFF, ray);

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
}
```

For procedural AABB geometry, handle each `CANDIDATE_PROCEDURAL_PRIMITIVE` during `Proceed()` and call `CommitProceduralPrimitiveHit` for an accepted intersection.

See [Bindless Resources](../fundamentals/bindless-resources.md) for the C#/Slang handle contract. Use a [timeline dependency](../fundamentals/synchronization.md#order-work-across-queues) when building and tracing on different queues.
