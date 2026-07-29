# Ray Tracing

Zenith.NET Ray Tracing uses bottom-level and top-level acceleration structures with inline Slang `RayQuery` operations.

## Check Support

Check `context.Capabilities.RayTracingSupported` before creating Ray Tracing resources.

## Build a BLAS

A bottom-level acceleration structure (BLAS) contains triangle or axis-aligned bounding-box geometry. Set `Geometries` and `BuildFlags` in its description, then record the build:

```csharp
BottomLevelAccelerationStructureDesc desc = new()
{
    Geometries = geometries,
    BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
};

BottomLevelAccelerationStructure resource = commandBuffer.BuildAccelerationStructure(desc);
```

Use `RayTracingGeometry.Aabbs` instead when the geometry is represented by bounding boxes.

## Build a TLAS

The top-level acceleration structure (TLAS) contains instances of existing BLAS objects. Set `Instances` and `BuildFlags` in its description, then record the build:

```csharp
TopLevelAccelerationStructureDesc desc = new()
{
    Instances = instances,
    BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
};

TopLevelAccelerationStructure resource = commandBuffer.BuildAccelerationStructure(desc);
```

Submit the build before tracing against the TLAS. Work submitted to the same queue remains ordered; when tracing on another queue, pass the build submission's `TimelineValue` to the tracing submission.

Keep every referenced BLAS alive while the TLAS is in use.

## Update an Acceleration Structure

Set `AllowUpdate` on the initial build and every later in-place update, and pass the updated description to `UpdateAccelerationStructure`.

Choose `PreferFastTrace`, `PreferFastBuild`, or `MinimizeMemory` according to how the structure is used.

## Trace Rays in Slang

Store the TLAS handle in constant data and declare the matching field as `DescriptorHandle<RaytracingAccelerationStructure>` in Slang.

For triangle geometry, initialize a `RayQuery`, trace it, and inspect the committed result:

```slang
RayDesc ray;
ray.Origin = origin;
ray.Direction = direction;
ray.TMin = 0.001;
ray.TMax = 100000.0;

RayQuery<RAY_FLAG_NONE> query;
query.TraceRayInline(*constants.Resource, RAY_FLAG_NONE, 0xFF, ray);

while (query.Proceed())
{
}

bool hit = query.CommittedStatus() != COMMITTED_NOTHING;
```

For procedural AABB geometry, handle each `CANDIDATE_PROCEDURAL_PRIMITIVE` during `Proceed()` and call `CommitProceduralPrimitiveHit` for an accepted intersection.

See [Shaders](../fundamentals/shaders.md) for compiling the entry point and [Bindless Resources](../fundamentals/bindless-resources.md) for the C#/Slang handle contract. Use a [timeline dependency](../fundamentals/synchronization.md#order-work-across-queues) when building and tracing on different queues.
