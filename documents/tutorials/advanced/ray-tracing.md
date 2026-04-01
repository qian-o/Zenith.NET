# Ray Tracing

In this tutorial, you'll learn how to use hardware-accelerated ray tracing with Zenith.NET. We'll render a scene with a checkered floor and a sphere, demonstrating acceleration structure construction and compute-shader-based ray tracing with `RayQuery`.

> [!NOTE]
> This tutorial requires a GPU with ray tracing support. Check `Context.Capabilities.RayTracingSupported` before using ray tracing features.

## Overview

We'll create a `RayTracingRenderer` class that:

- Creates floor geometry using triangle buffers
- Creates a sphere using procedural AABBs
- Builds separate BLAS for floor and sphere, combined in a TLAS
- Uses a compute shader with `RayQuery` for ray tracing
- Implements shadow rays for hard shadows
- Copies the result to the swap chain for display

## Key Concepts

### Ray Tracing with RayQuery

Zenith.NET uses `RayQuery` for ray tracing. This approach binds the acceleration structure as a regular resource and performs all ray tracing logic within a single shader:

| Aspect | Description |
|--------|-------------|
| **Shader Stage** | Any shader (typically compute) |
| **Setup** | Bind acceleration structure to any pipeline |
| **Hit/Miss Logic** | All logic in one shader using `RayQuery` API |
| **Best For** | Shadows, AO, GI, reflections — any ray tracing workload |

### Acceleration Structures

Ray tracing uses a two-level acceleration structure hierarchy:

- **BLAS (Bottom-Level Acceleration Structure)**: Contains the actual geometry data. Each BLAS can store either triangle meshes or axis-aligned bounding boxes (AABBs) for procedural geometry.
- **TLAS (Top-Level Acceleration Structure)**: Contains instances that reference one or more BLAS with transform matrices. Multiple instances can share the same BLAS with different transforms.

```
TLAS (scene)
├── Instance 0 → BLAS 0 (floor, triangles)
├── Instance 1 → BLAS 1 (sphere, AABBs)
├── Instance 2 → BLAS 0 (same geometry, different transform)
└── ...
```

> [!IMPORTANT]
> Acceleration structure transforms only support rotation and scale. Translation is **not supported** - use the geometry's world-space coordinates directly.

## The Renderer Class

Create a new file `Renderers/RayTracingRenderer.cs`:

```csharp
namespace ZenithTutorials.Renderers;

internal unsafe class RayTracingRenderer : IRenderer
{
    private const uint ThreadGroupSize = 16;

    private const string ShaderSource = """
        struct Sphere
        {
            private float4 CenterAndRadius;

            private float4 ColorAndPadding;

            property float3 Center { get { return CenterAndRadius.xyz; } }

            property float Radius { get { return CenterAndRadius.w; } }

            property float3 Color { get { return ColorAndPadding.xyz; } }
        };

        RaytracingAccelerationStructure scene;
        StructuredBuffer<Sphere> spheres;
        RWTexture2D<float4> outputTexture;

        static const float3 LightDir = normalize(float3(1.0, 1.0, -0.5));
        static const float3 LightColor = float3(1.0, 0.98, 0.95);
        static const float3 AmbientColor = float3(0.1, 0.1, 0.15);

        float IntersectSphere(float3 origin, float3 direction, Sphere sphere)
        {
            float3 oc = origin - sphere.Center;

            float a = dot(direction, direction);
            float b = dot(oc, direction);
            float c = dot(oc, oc) - sphere.Radius * sphere.Radius;
            float discriminant = b * b - a * c;

            if (discriminant > 0.0)
            {
                float sqrtD = sqrt(discriminant);
                float t1 = (-b - sqrtD) / a;

                if (t1 > 0.0)
                {
                    return t1;
                }

                float t2 = (-b + sqrtD) / a;

                if (t2 > 0.0)
                {
                    return t2;
                }
            }

            return -1.0;
        }

        bool TraceShadowRay(float3 origin, float3 direction)
        {
            RayDesc shadowRay;
            shadowRay.Origin = origin;
            shadowRay.Direction = direction;
            shadowRay.TMin = 0.001;
            shadowRay.TMax = 1000.0;

            RayQuery<RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH> shadowQuery;
            shadowQuery.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, shadowRay);

            while (shadowQuery.Proceed())
            {
                if (shadowQuery.CandidateType() == CANDIDATE_PROCEDURAL_PRIMITIVE)
                {
                    uint sphereIndex = shadowQuery.CandidatePrimitiveIndex();
                    Sphere sphere = spheres[sphereIndex];

                    float3 ro = shadowQuery.CandidateObjectRayOrigin();
                    float3 rd = shadowQuery.CandidateObjectRayDirection();

                    float t = IntersectSphere(ro, rd, sphere);

                    if (t >= shadowQuery.RayTMin() && t <= shadowQuery.CommittedRayT())
                    {
                        shadowQuery.CommitProceduralPrimitiveHit(t);
                    }
                }
            }

            return shadowQuery.CommittedStatus() != COMMITTED_NOTHING;
        }

        [numthreads(16, 16, 1)]
        void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
        {
            uint2 pixelCoord = dispatchThreadID.xy;

            uint width, height;
            outputTexture.GetDimensions(width, height);

            if (pixelCoord.x >= width || pixelCoord.y >= height)
            {
                return;
            }

            float2 uv = (float2(pixelCoord) + 0.5) / float2(width, height);
            float2 ndc = uv * 2.0 - 1.0;
            ndc.y = -ndc.y;

            float aspectRatio = float(width) / float(height);
            float fov = tan(radians(45.0) * 0.5);

            float3 cameraPos = float3(0.0, 4.0, -12.0);
            float3 cameraTarget = float3(0.0, 0.0, 0.0);
            float3 cameraUp = float3(0.0, 1.0, 0.0);

            float3 forward = normalize(cameraTarget - cameraPos);
            float3 right = normalize(cross(forward, cameraUp));
            float3 up = cross(right, forward);

            float3 rayDir = normalize(forward + ndc.x * aspectRatio * fov * right + ndc.y * fov * up);

            RayDesc ray;
            ray.Origin = cameraPos;
            ray.Direction = rayDir;
            ray.TMin = 0.001;
            ray.TMax = 1000.0;

            float3 sphereHitNormal = float3(0.0);
            float3 sphereHitColor = float3(0.0);

            RayQuery<RAY_FLAG_NONE> query;
            query.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, ray);

            while (query.Proceed())
            {
                if (query.CandidateType() == CANDIDATE_PROCEDURAL_PRIMITIVE)
                {
                    uint sphereIndex = query.CandidatePrimitiveIndex();
                    Sphere sphere = spheres[sphereIndex];

                    float3 ro = query.CandidateObjectRayOrigin();
                    float3 rd = query.CandidateObjectRayDirection();

                    float t = IntersectSphere(ro, rd, sphere);

                    if (t >= query.RayTMin() && t <= query.CommittedRayT())
                    {
                        float3 hitPoint = ro + rd * t;

                        sphereHitNormal = normalize(hitPoint - sphere.Center);
                        sphereHitColor = sphere.Color;

                        query.CommitProceduralPrimitiveHit(t);
                    }
                }
            }

            float3 color;

            if (query.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
            {
                float3 hitPoint = ray.Origin + ray.Direction * query.CommittedRayT();

                float scale = 1.0;
                int checkX = int(floor(hitPoint.x * scale));
                int checkZ = int(floor(hitPoint.z * scale));
                bool isWhite = ((checkX + checkZ) & 1) == 0;
                float3 baseColor = isWhite ? float3(0.9, 0.9, 0.9) : float3(0.2, 0.2, 0.2);

                float3 normal = float3(0.0, 1.0, 0.0);
                float NdotL = max(dot(normal, LightDir), 0.0);

                float3 shadowOrigin = hitPoint + normal * 0.001;
                bool inShadow = TraceShadowRay(shadowOrigin, LightDir);

                float shadow = inShadow ? 0.3 : 1.0;
                float3 diffuse = baseColor * LightColor * NdotL * shadow;
                float3 ambient = baseColor * AmbientColor;

                color = ambient + diffuse;
            }
            else if (query.CommittedStatus() == COMMITTED_PROCEDURAL_PRIMITIVE_HIT)
            {
                float3 hitPoint = ray.Origin + ray.Direction * query.CommittedRayT();

                float NdotL = max(dot(sphereHitNormal, LightDir), 0.0);

                float3 shadowOrigin = hitPoint + sphereHitNormal * 0.001;
                bool inShadow = TraceShadowRay(shadowOrigin, LightDir);

                float shadow = inShadow ? 0.3 : 1.0;
                float3 diffuse = sphereHitColor * LightColor * NdotL * shadow;
                float3 ambient = sphereHitColor * AmbientColor;

                color = ambient + diffuse;
            }
            else
            {
                float t = 0.5 * (rayDir.y + 1.0);

                color = lerp(float3(1.0, 1.0, 1.0), float3(0.5, 0.7, 1.0), t);
            }

            color = pow(color, 1.0 / 2.2);

            outputTexture[pixelCoord] = float4(color, 1.0);
        }
        """;

    private readonly Buffer floorVertexBuffer;
    private readonly Buffer floorIndexBuffer;
    private readonly Buffer sphereBuffer;
    private readonly Buffer aabbBuffer;
    private readonly BottomLevelAccelerationStructure floorBlas;
    private readonly BottomLevelAccelerationStructure sphereBlas;
    private readonly TopLevelAccelerationStructure tlas;
    private readonly ResourceLayout resourceLayout;
    private readonly ComputePipeline pipeline;
    private Texture? outputTexture;
    private ResourceTable? resourceTable;

    public RayTracingRenderer()
    {
        if (!App.Context.Capabilities.RayTracingSupported)
        {
            throw new NotSupportedException("Ray tracing is not supported on this device.");
        }

        Vector3[] floorVertices =
        [
            new(-5.0f, 0.0f, -5.0f),
            new( 5.0f, 0.0f, -5.0f),
            new( 5.0f, 0.0f,  5.0f),
            new(-5.0f, 0.0f,  5.0f)
        ];
        uint[] floorIndices = [0, 1, 2, 0, 2, 3];

        floorVertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vector3) * floorVertices.Length),
            StrideInBytes = (uint)sizeof(Vector3),
            Flags = BufferUsageFlags.Vertex | BufferUsageFlags.AccelerationStructure
        });
        floorVertexBuffer.Upload(floorVertices, 0);

        floorIndexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * floorIndices.Length),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.Index | BufferUsageFlags.AccelerationStructure
        });
        floorIndexBuffer.Upload(floorIndices, 0);

        Sphere[] sphereData =
        [
            new() { Center = new(-1.5f, 1.0f, 0.0f), Radius = 1.0f, Color = new(0.8f, 0.2f, 0.2f) },
            new() { Center = new( 1.5f, 1.0f, 0.0f), Radius = 1.0f, Color = new(0.2f, 0.4f, 0.8f) }
        ];

        sphereBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Sphere) * sphereData.Length),
            StrideInBytes = (uint)sizeof(Sphere),
            Flags = BufferUsageFlags.ShaderResource
        });
        sphereBuffer.Upload(sphereData, 0);

        Vector3[] aabbData = new Vector3[sphereData.Length * 2];
        for (int i = 0; i < sphereData.Length; i++)
        {
            aabbData[i * 2] = sphereData[i].Center - new Vector3(sphereData[i].Radius);
            aabbData[(i * 2) + 1] = sphereData[i].Center + new Vector3(sphereData[i].Radius);
        }

        aabbBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vector3) * aabbData.Length),
            StrideInBytes = (uint)(sizeof(Vector3) * 2),
            Flags = BufferUsageFlags.ShaderResource | BufferUsageFlags.AccelerationStructure
        });
        aabbBuffer.Upload(aabbData, 0);

        CommandBuffer buildCmd = App.Context.Graphics.CommandBuffer();

        floorBlas = buildCmd.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
        {
            Geometries =
            [
                new()
                {
                    Type = RayTracingGeometryType.Triangles,
                    Triangles = new()
                    {
                        VertexBuffer = floorVertexBuffer,
                        VertexFormat = PixelFormat.R32G32B32Float,
                        VertexCount = (uint)floorVertices.Length,
                        VertexStrideInBytes = (uint)sizeof(Vector3),
                        IndexBuffer = floorIndexBuffer,
                        IndexFormat = IndexFormat.UInt32,
                        IndexCount = (uint)floorIndices.Length,
                        Transform = Matrix4x4.Identity
                    },
                    Flags = RayTracingGeometryFlags.Opaque
                }
            ],
            Flags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        sphereBlas = buildCmd.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
        {
            Geometries =
            [
                new()
                {
                    Type = RayTracingGeometryType.AABBs,
                    AABBs = new()
                    {
                        Buffer = aabbBuffer,
                        Count = (uint)sphereData.Length,
                        StrideInBytes = (uint)(sizeof(Vector3) * 2)
                    },
                    Flags = RayTracingGeometryFlags.Opaque
                }
            ],
            Flags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        tlas = buildCmd.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
        {
            Instances =
            [
                new()
                {
                    AccelerationStructure = floorBlas,
                    ID = 0,
                    Mask = 0xFF,
                    Transform = Matrix4x4.Identity,
                    Flags = RayTracingInstanceFlags.None
                },
                new()
                {
                    AccelerationStructure = sphereBlas,
                    ID = 1,
                    Mask = 0xFF,
                    Transform = Matrix4x4.Identity,
                    Flags = RayTracingInstanceFlags.None
                }
            ],
            Flags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        buildCmd.Submit(waitForCompletion: true);

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = BindingHelper.Bindings
            (
                new() { Type = ResourceType.AccelerationStructure, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
        });

        using Shader computeShader = App.Context.LoadShaderFromSource(ShaderSource, "CSMain", ShaderStageFlags.Compute);

        pipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = computeShader,
            ResourceLayout = resourceLayout,
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
        });
    }

    public void Update(double deltaTime)
    {
    }

    public void Render()
    {
        outputTexture ??= App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.B8G8R8A8UNorm,
            Width = App.Width,
            Height = App.Height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
        });

        resourceTable ??= App.Context.CreateResourceTable(new()
        {
            Layout = resourceLayout,
            Resources = [tlas, sphereBuffer, outputTexture]
        });

        CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetResourceTable(resourceTable);

        uint dispatchX = (App.Width + ThreadGroupSize - 1) / ThreadGroupSize;
        uint dispatchY = (App.Height + ThreadGroupSize - 1) / ThreadGroupSize;

        commandBuffer.Dispatch(dispatchX, dispatchY, 1);

        Texture colorTarget = App.SwapChain.FrameBuffer.Desc.ColorAttachments[0].Target;

        commandBuffer.CopyTexture(outputTexture,
                                  default,
                                  default,
                                  colorTarget,
                                  default,
                                  default,
                                  new() { Width = App.Width, Height = App.Height, Depth = 1 });

        commandBuffer.Submit(waitForCompletion: true);
    }

    public void Resize(uint width, uint height)
    {
        resourceTable?.Dispose();
        resourceTable = null;
        outputTexture?.Dispose();
        outputTexture = null;
    }

    public void Dispose()
    {
        resourceTable?.Dispose();
        outputTexture?.Dispose();

        pipeline.Dispose();
        resourceLayout.Dispose();
        tlas.Dispose();
        sphereBlas.Dispose();
        floorBlas.Dispose();
        aabbBuffer.Dispose();
        sphereBuffer.Dispose();
        floorIndexBuffer.Dispose();
        floorVertexBuffer.Dispose();
    }
}

/// <summary>
/// Sphere definition for procedural geometry.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct Sphere
{
    [FieldOffset(0)]
    public Vector3 Center;

    [FieldOffset(12)]
    public float Radius;

    [FieldOffset(16)]
    public Vector3 Color;
}
```

## Running the Tutorial

Update your `Program.cs` to run the `RayTracingRenderer`:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

App.Run<RayTracingRenderer>();

App.Cleanup();
```

Run the application:

```bash
dotnet run
```

## Result

![ray-tracing](../../images/ray-tracing.png)

## Code Breakdown

### Checking Ray Tracing Support

```csharp
if (!App.Context.Capabilities.RayTracingSupported)
{
    throw new NotSupportedException("Ray tracing is not supported on this device.");
}
```

Always check `Capabilities.RayTracingSupported` before using ray tracing features.

### Acceleration Structure Setup

Build a two-level acceleration structure hierarchy:

```csharp
// Floor BLAS (triangle geometry)
floorBlas = buildCmd.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
{
    Geometries =
    [
        new()
        {
            Type = RayTracingGeometryType.Triangles,
            Triangles = new()
            {
                VertexBuffer = floorVertexBuffer,
                VertexFormat = PixelFormat.R32G32B32Float,
                VertexCount = (uint)floorVertices.Length,
                VertexStrideInBytes = (uint)sizeof(Vector3),
                IndexBuffer = floorIndexBuffer,
                IndexFormat = IndexFormat.UInt32,
                IndexCount = (uint)floorIndices.Length,
                Transform = Matrix4x4.Identity
            },
            Flags = RayTracingGeometryFlags.Opaque
        }
    ],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});

// Sphere BLAS (procedural AABB geometry)
sphereBlas = buildCmd.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
{
    Geometries =
    [
        new()
        {
            Type = RayTracingGeometryType.AABBs,
            AABBs = new()
            {
                Buffer = aabbBuffer,
                Count = (uint)sphereData.Length,
                StrideInBytes = (uint)(sizeof(Vector3) * 2)
            },
            Flags = RayTracingGeometryFlags.Opaque
        }
    ],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});
```

Combine BLAS into a TLAS:

```csharp
tlas = buildCmd.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
{
    Instances =
    [
        new()
        {
            AccelerationStructure = floorBlas,
            ID = 0,
            Mask = 0xFF,
            Transform = Matrix4x4.Identity,
            ...
        },
        new()
        {
            AccelerationStructure = sphereBlas,
            ID = 1,
            Mask = 0xFF,
            Transform = Matrix4x4.Identity,
            ...
        }
    ],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});
```

### Ray Tracing with RayQuery

Ray tracing in Zenith.NET uses `RayQuery` within a compute shader. Procedural geometry requires a custom intersection function:

```slang
float IntersectSphere(float3 origin, float3 direction, Sphere sphere)
{
    float3 oc = origin - sphere.Center;

    float a = dot(direction, direction);
    float b = dot(oc, direction);
    float c = dot(oc, oc) - sphere.Radius * sphere.Radius;
    float discriminant = b * b - a * c;

    if (discriminant > 0.0)
    {
        float sqrtD = sqrt(discriminant);
        float t1 = (-b - sqrtD) / a;

        if (t1 > 0.0)
        {
            return t1;
        }

        float t2 = (-b + sqrtD) / a;

        if (t2 > 0.0)
        {
            return t2;
        }
    }

    return -1.0;
}
```

The main ray query loop processes only procedural candidates, storing hit information for later use:

```slang
float3 sphereHitNormal = float3(0.0);
float3 sphereHitColor = float3(0.0);

RayQuery<RAY_FLAG_NONE> query;
query.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, ray);

while (query.Proceed())
{
    if (query.CandidateType() == CANDIDATE_PROCEDURAL_PRIMITIVE)
    {
        uint sphereIndex = query.CandidatePrimitiveIndex();
        Sphere sphere = spheres[sphereIndex];

        float3 ro = query.CandidateObjectRayOrigin();
        float3 rd = query.CandidateObjectRayDirection();

        float t = IntersectSphere(ro, rd, sphere);

        if (t >= query.RayTMin() && t <= query.CommittedRayT())
        {
            float3 hitPoint = ro + rd * t;

            sphereHitNormal = normalize(hitPoint - sphere.Center);
            sphereHitColor = sphere.Color;

            query.CommitProceduralPrimitiveHit(t);
        }
    }
}

// Check result
if (query.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
{
    // Handle triangle hit
}
else if (query.CommittedStatus() == COMMITTED_PROCEDURAL_PRIMITIVE_HIT)
{
    // Handle procedural hit using stored normal and color
}
```

Key elements:

| Element | Description |
|---------|-------------|
| `RayQuery<FLAGS>` | Declares a ray query with template ray flags |
| `TraceRayInline` | Initiates the ray traversal |
| `Proceed()` | Advances traversal; returns `true` while candidates remain |
| `CandidateType()` | Returns the type of the current candidate hit |
| `CommitNonOpaqueTriangleHit()` | Accepts a triangle hit |
| `CommitProceduralPrimitiveHit(t)` | Accepts a procedural hit at distance `t` |
| `CommittedStatus()` | Returns the final hit result after traversal |

### Shadow Rays with RayQuery

Shadow rays test visibility to a light source. Note that shadow rays must also handle procedural geometry intersections:

```slang
bool TraceShadowRay(float3 origin, float3 direction)
{
    RayDesc shadowRay;
    shadowRay.Origin = origin;
    shadowRay.Direction = direction;
    shadowRay.TMin = 0.001;
    shadowRay.TMax = 1000.0;

    RayQuery<RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH> shadowQuery;
    shadowQuery.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, shadowRay);

    while (shadowQuery.Proceed())
    {
        if (shadowQuery.CandidateType() == CANDIDATE_PROCEDURAL_PRIMITIVE)
        {
            uint sphereIndex = shadowQuery.CandidatePrimitiveIndex();
            Sphere sphere = spheres[sphereIndex];

            float3 ro = shadowQuery.CandidateObjectRayOrigin();
            float3 rd = shadowQuery.CandidateObjectRayDirection();

            float t = IntersectSphere(ro, rd, sphere);

            if (t >= shadowQuery.RayTMin() && t <= shadowQuery.CommittedRayT())
            {
                shadowQuery.CommitProceduralPrimitiveHit(t);
            }
        }
    }

    return shadowQuery.CommittedStatus() != COMMITTED_NOTHING;
}
```

Key optimization: `RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH` stops at the first hit since we only need to know if something blocks the light.

### Compute Pipeline for Ray Tracing

```csharp
resourceLayout = App.Context.CreateResourceLayout(new()
{
    Bindings = BindingHelper.Bindings
    (
        new() { Type = ResourceType.AccelerationStructure, Count = 1, StageFlags = ShaderStageFlags.Compute },
        new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
        new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute }
    )
});

pipeline = App.Context.CreateComputePipeline(new()
{
    Compute = computeShader,
    ResourceLayout = resourceLayout,
    ThreadGroupSizeX = ThreadGroupSize,
    ThreadGroupSizeY = ThreadGroupSize,
    ThreadGroupSizeZ = 1
});
```

Note that all resource bindings use `ShaderStageFlags.Compute` since ray tracing runs entirely within a compute shader.

### Dispatching and Display

```csharp
uint dispatchX = (App.Width + ThreadGroupSize - 1) / ThreadGroupSize;
uint dispatchY = (App.Height + ThreadGroupSize - 1) / ThreadGroupSize;

commandBuffer.SetPipeline(pipeline);
commandBuffer.SetResourceTable(resourceTable);
commandBuffer.Dispatch(dispatchX, dispatchY, 1);
```

The compute shader processes each pixel independently. The result is then copied to the swap chain's color target for display.

## Next Steps

- [Mesh Shading](mesh-shading.md) - Process geometry in meshlets using the modern mesh shading pipeline

## Source Code

> [!TIP]
> View the complete source code on GitHub: [RayTracingRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs)
