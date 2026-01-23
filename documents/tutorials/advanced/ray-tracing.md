# Ray Tracing

In this tutorial, you'll learn how to use hardware-accelerated ray tracing with Zenith.NET. We'll render a classic colored triangle using ray tracing instead of rasterization.

> [!NOTE]
> This tutorial requires a GPU with ray tracing support. Check `Context.Capabilities.RayTracingSupported` before using ray tracing features.

## Overview

We'll create a `RayTracingRenderer` class that:

- Creates a vertex buffer for a single triangle
- Builds BLAS and TLAS acceleration structures
- Creates a ray tracing pipeline with ray generation, miss, and closest hit shaders
- Renders a colored triangle using ray tracing

## Key Concepts

### Two Ways to Use Ray Tracing

There are two approaches to use hardware ray tracing:

| Aspect | Ray Tracing Pipeline | Inline Ray Tracing (RayQuery) |
|--------|----------------------|-------------------------------|
| **Shader Stages** | RayGen, Miss, ClosestHit, AnyHit, Intersection | Any shader (CS, PS, etc.) |
| **Setup Complexity** | Requires dedicated pipeline and hit groups | Bind acceleration structure only |
| **Hit/Miss Logic** | Separated into different shaders | All logic in one shader |
| **Best For** | Complex materials, multiple ray types | Simple queries, shadows, AO |

This tutorial covers the **Ray Tracing Pipeline** approach. For Inline Ray Tracing, simply bind the acceleration structure to your compute/graphics pipeline and use `RayQuery` in your shader.

### Acceleration Structures

Ray tracing uses a two-level hierarchy to efficiently find ray-geometry intersections:



```
Bottom-Level AS (BLAS)
├── BLAS 0: Cube geometry
├── BLAS 1: Sphere geometry
└── ...

Top-Level AS (TLAS)
├── Instance 0 → BLAS 0
├── Instance 1 → BLAS 0  ← same BLAS, different transform
├── Instance 2 → BLAS 1
└── ...
```

- **BLAS (Bottom-Level)**: Contains the actual geometry. Can store triangles or axis-aligned bounding boxes (AABBs) for procedural geometry.
- **TLAS (Top-Level)**: Contains instances that reference BLAS with a transform matrix. Multiple instances can share the same BLAS.

> [!NOTE]
> Acceleration structure transforms do not support translation.

### Ray Tracing Pipeline Stages

| Shader Stage | When Called |
|--------------|-------------|
| **Ray Generation** | Entry point - invoked for each pixel/thread |
| **Intersection** | For procedural geometry (AABBs) to compute ray-geometry intersection |
| **Any Hit** | For each potential intersection - can accept/reject hit (alpha testing) |
| **Closest Hit** | Once per ray, for the nearest accepted intersection |
| **Miss** | When the ray hits nothing |

### Hit Groups

Hit Groups bundle shaders that work together for a specific geometry type:

| Shader | Required | Description |
|--------|----------|-------------|
| **ClosestHit** | Optional | Called for the closest intersection point |
| **AnyHit** | Optional | Called for each potential hit (alpha testing, transparency) |
| **Intersection** | Optional | Custom intersection for procedural geometry. Required only for AABBs, triangles use built-in intersection. |

## The Renderer Class

Create a new file `Renderers/RayTracingRenderer.cs`:

```csharp
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace ZenithTutorials.Renderers;

internal unsafe class RayTracingRenderer : IRenderer
{
    /// <summary>
    /// Vertex structure with position and color data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex(Vector3 position, Vector4 color)
    {
        public Vector3 Position = position;

        public Vector4 Color = color;
    }

    private const string shaderSource = """
        struct Vertex
        {
            float3 Position;

            float4 Color;
        };

        struct Payload
        {
            float4 Color;
        };

        RaytracingAccelerationStructure scene;
        RWTexture2D<float4> outputTexture;
        StructuredBuffer<Vertex> vertices;

        [shader("raygeneration")]
        void RayGen()
        {
            uint2 pixelCoord = DispatchRaysIndex().xy;
            uint2 dimensions = DispatchRaysDimensions().xy;

            // Calculate ray direction from pixel coordinates (orthographic-like for simplicity)
            float2 uv = (float2(pixelCoord) + 0.5) / float2(dimensions);
            float2 ndc = uv * 2.0 - 1.0;

            float3 rayOrigin = float3(ndc.x, -ndc.y, -1.0);
            float3 rayDir = float3(0, 0, 1);

            RayDesc ray;
            ray.Origin = rayOrigin;
            ray.Direction = rayDir;
            ray.TMin = 0.001;
            ray.TMax = 100.0;

            Payload payload;
            payload.Color = float4(0, 0, 0, 1);

            TraceRay(scene, RAY_FLAG_NONE, 0xFF, 0, 0, 0, ray, payload);

            outputTexture[pixelCoord] = payload.Color;
        }

        [shader("miss")]
        void Miss(inout Payload payload)
        {
            // Sky gradient background
            float3 rayDir = WorldRayDirection();
            float t = 0.5 * (rayDir.y + 1.0);
            payload.Color = float4(lerp(float3(1.0, 1.0, 1.0), float3(0.5, 0.7, 1.0), t), 1.0);
        }

        [shader("closesthit")]
        void ClosestHit(inout Payload payload, BuiltInTriangleIntersectionAttributes attribs)
        {
            // Interpolate vertex colors using barycentric coordinates
            float3 barycentrics = float3(1.0 - attribs.barycentrics.x - attribs.barycentrics.y,
                                         attribs.barycentrics.x,
                                         attribs.barycentrics.y);

            uint primitiveIndex = PrimitiveIndex();
            uint baseIndex = primitiveIndex * 3;

            float4 color0 = vertices[baseIndex + 0].Color;
            float4 color1 = vertices[baseIndex + 1].Color;
            float4 color2 = vertices[baseIndex + 2].Color;

            payload.Color = color0 * barycentrics.x + color1 * barycentrics.y + color2 * barycentrics.z;
        }
        """;

    private readonly Buffer vertexBuffer;
    private readonly BottomLevelAccelerationStructure blas;
    private readonly TopLevelAccelerationStructure tlas;
    private readonly ResourceLayout resourceLayout;
    private readonly RayTracingPipeline pipeline;

    // For displaying the result
    private readonly Sampler sampler;
    private readonly ResourceLayout displayResourceLayout;
    private readonly GraphicsPipeline displayPipeline;
    private readonly Buffer quadVertexBuffer;
    private readonly Buffer quadIndexBuffer;

    // Resizable resources
    private Texture? outputTexture;
    private ResourceSet? resourceSet;
    private ResourceSet? displayResourceSet;

    public RayTracingRenderer()
    {
        if (!App.Context.Capabilities.RayTracingSupported)
        {
            throw new NotSupportedException("Ray tracing is not supported on this device.");
        }

        // Create triangle vertices (same as Hello Triangle, but for ray tracing)
        Vertex[] vertices =
        [
            new(new( 0.0f,  0.5f, 0.0f), new(1.0f, 0.0f, 0.0f, 1.0f)),  // Top - Red
            new(new( 0.5f, -0.5f, 0.0f), new(0.0f, 1.0f, 0.0f, 1.0f)),  // Right - Green
            new(new(-0.5f, -0.5f, 0.0f), new(0.0f, 0.0f, 1.0f, 1.0f))   // Left - Blue
        ];

        vertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length),
            StrideInBytes = (uint)sizeof(Vertex),
            Flags = BufferUsageFlags.AccelerationStructure | BufferUsageFlags.ShaderResource
        });
        vertexBuffer.Upload(vertices, 0);

        // Build acceleration structures
        CommandBuffer buildCmd = App.Context.Graphics.CommandBuffer();

        blas = buildCmd.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
        {
            Geometries =
            [
                new RayTracingGeometry
                {
                    Type = RayTracingGeometryType.Triangles,
                    Triangles = new RayTracingTriangles
                    {
                        VertexBuffer = vertexBuffer,
                        VertexFormat = PixelFormat.R32G32B32Float,
                        VertexCount = (uint)vertices.Length,
                        VertexStrideInBytes = (uint)sizeof(Vertex),
                        VertexOffsetInBytes = 0,
                        Transform = Matrix4x4.Identity
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
                new RayTracingInstance
                {
                    AccelerationStructure = blas,
                    InstanceID = 0,
                    InstanceMask = 0xFF,
                    InstanceContributionToHitGroupIndex = 0,
                    Transform = Matrix4x4.Identity,
                    Flags = RayTracingInstanceFlags.None
                }
            ],
            Flags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        buildCmd.Submit(waitForCompletion: true);

        // Create resource layout for ray tracing
        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = BindingHelper.Bindings
            (
                new()
                {
                    Type = ResourceType.AccelerationStructure,
                    Count = 1,
                    StageFlags = ShaderStageFlags.RayGeneration
                },
                new()
                {
                    Type = ResourceType.TextureReadWrite,
                    Count = 1,
                    StageFlags = ShaderStageFlags.RayGeneration
                },
                new()
                {
                    Type = ResourceType.StructuredBuffer,
                    Count = 1,
                    StageFlags = ShaderStageFlags.ClosestHit
                }
            )
        });

        // Compile ray tracing shaders
        using Shader rayGenShader = App.Context.LoadShaderFromSource(shaderSource,
                                                                     "RayGen",
                                                                     ShaderStageFlags.RayGeneration);
        using Shader missShader = App.Context.LoadShaderFromSource(shaderSource,
                                                                   "Miss",
                                                                   ShaderStageFlags.Miss);
        using Shader closestHitShader = App.Context.LoadShaderFromSource(shaderSource,
                                                                         "ClosestHit",
                                                                         ShaderStageFlags.ClosestHit);

        // Create ray tracing pipeline
        pipeline = App.Context.CreateRayTracingPipeline(new()
        {
            RayGeneration = rayGenShader,
            Miss = [missShader],
            AnyHit = [],
            Intersection = [],
            ClosestHit = [closestHitShader],
            HitGroups =
            [
                new HitGroup
                {
                    Type = HitGroupType.Triangles,
                    Name = "TriangleHitGroup",
                    ClosestHit = "ClosestHit"
                }
            ],
            ResourceLayouts = [resourceLayout],
            MaxTraceRecursionDepth = 1,
            MaxPayloadSizeInBytes = 16,
            MaxAttributeSizeInBytes = 8
        });

        // Create display resources (fullscreen quad to show ray tracing result)
        sampler = App.Context.CreateSampler(new()
        {
            Filter = Filter.MinLinearMagLinearMipLinear,
            U = AddressMode.Clamp,
            V = AddressMode.Clamp,
            W = AddressMode.Clamp,
            MaxLod = uint.MaxValue
        });

        displayResourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = BindingHelper.Bindings
            (
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Pixel }
            )
        });

        const string displayShaderSource = """
            struct VSInput
            {
                float3 Position : POSITION0;

                float2 TexCoord : TEXCOORD0;
            };

            struct PSInput
            {
                float4 Position : SV_POSITION;

                float2 TexCoord : TEXCOORD0;
            };

            Texture2D displayTexture;
            SamplerState samplerState;

            PSInput VSMain(VSInput input)
            {
                PSInput output;
                output.Position = float4(input.Position, 1.0);
                output.TexCoord = input.TexCoord;

                return output;
            }

            float4 PSMain(PSInput input) : SV_TARGET
            {
                return displayTexture.Sample(samplerState, input.TexCoord);
            }
            """;

        using Shader displayVS = App.Context.LoadShaderFromSource(displayShaderSource,
                                                                  "VSMain",
                                                                  ShaderStageFlags.Vertex);
        using Shader displayPS = App.Context.LoadShaderFromSource(displayShaderSource,
                                                                  "PSMain",
                                                                  ShaderStageFlags.Pixel);

        InputLayout displayInputLayout = new();
        displayInputLayout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
        displayInputLayout.Add(new() { Format = ElementFormat.Float2, Semantic = ElementSemantic.TexCoord });

        displayPipeline = App.Context.CreateGraphicsPipeline(new()
        {
            RenderStates = new()
            {
                RasterizerState = RasterizerStates.CullNone,
                DepthStencilState = DepthStencilStates.None,
                BlendState = BlendStates.Opaque
            },
            Vertex = displayVS,
            Pixel = displayPS,
            ResourceLayouts = [displayResourceLayout],
            InputLayouts = [displayInputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = App.SwapChain.FrameBuffer.Output
        });

        // Fullscreen quad vertices
        float[] quadVertices =
        [
            -1,  1, 0, 0, 0,
             1,  1, 0, 1, 0,
             1, -1, 0, 1, 1,
            -1, -1, 0, 0, 1
        ];
        uint[] quadIndices = [0, 1, 2, 0, 2, 3];

        quadVertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(float) * quadVertices.Length),
            StrideInBytes = sizeof(float) * 5,
            Flags = BufferUsageFlags.Vertex | BufferUsageFlags.MapWrite
        });
        quadVertexBuffer.Upload(quadVertices, 0);

        quadIndexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * quadIndices.Length),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.Index | BufferUsageFlags.MapWrite
        });
        quadIndexBuffer.Upload(quadIndices, 0);
    }

    public void Update(double deltaTime)
    {
    }

    public void Render()
    {
        // Ensure output texture exists
        outputTexture ??= App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8G8B8A8UNorm,
            Width = App.Width,
            Height = App.Height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
        });

        // Ensure resource sets exist
        resourceSet ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources = [tlas, outputTexture, vertexBuffer]
        });

        displayResourceSet ??= App.Context.CreateResourceSet(new()
        {
            Layout = displayResourceLayout,
            Resources = [outputTexture, sampler]
        });

        CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        // Ray tracing pass
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetResourceSet(resourceSet, 0);
        commandBuffer.DispatchRays(App.Width, App.Height, 1);

        // Display pass
        commandBuffer.BeginRenderPass(App.SwapChain.FrameBuffer, new()
        {
            ColorValues = [new(0, 0, 0, 1)],
            Depth = 1.0f,
            Stencil = 0,
            Flags = ClearFlags.All
        }, displayResourceSet);

        commandBuffer.SetPipeline(displayPipeline);
        commandBuffer.SetResourceSet(displayResourceSet, 0);
        commandBuffer.SetVertexBuffer(quadVertexBuffer, 0, 0);
        commandBuffer.SetIndexBuffer(quadIndexBuffer, 0, IndexFormat.UInt32);
        commandBuffer.DrawIndexed(6, 1, 0, 0, 0);

        commandBuffer.EndRenderPass();

        commandBuffer.Submit(waitForCompletion: true);
    }

    public void Resize(uint width, uint height)
    {
        resourceSet?.Dispose();
        resourceSet = null;
        displayResourceSet?.Dispose();
        displayResourceSet = null;

        outputTexture?.Dispose();
        outputTexture = null;
    }

    public void Dispose()
    {
        displayResourceSet?.Dispose();
        resourceSet?.Dispose();
        outputTexture?.Dispose();

        quadIndexBuffer.Dispose();
        quadVertexBuffer.Dispose();
        displayPipeline.Dispose();
        displayResourceLayout.Dispose();
        sampler.Dispose();

        pipeline.Dispose();
        resourceLayout.Dispose();
        tlas.Dispose();
        blas.Dispose();
        vertexBuffer.Dispose();
    }
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

### Building Acceleration Structures

```csharp
// Build BLAS (geometry)
blas = buildCmd.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
{
    Geometries =
    [
        new RayTracingGeometry
        {
            Type = RayTracingGeometryType.Triangles,
            Triangles = new RayTracingTriangles
            {
                VertexBuffer = vertexBuffer,
                VertexFormat = PixelFormat.R32G32B32Float,
                VertexCount = (uint)vertices.Length,
                VertexStrideInBytes = (uint)sizeof(Vertex),
                VertexOffsetInBytes = 0,
                Transform = Matrix4x4.Identity
            },
            Flags = RayTracingGeometryFlags.Opaque
        }
    ],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});

// Build TLAS (instances)
tlas = buildCmd.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
{
    Instances =
    [
        new RayTracingInstance
        {
            AccelerationStructure = blas,
            InstanceID = 0,
            InstanceMask = 0xFF,
            InstanceContributionToHitGroupIndex = 0,
            Transform = Matrix4x4.Identity,
            Flags = RayTracingInstanceFlags.None
        }
    ],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});
```

### Creating the Ray Tracing Pipeline

```csharp
pipeline = App.Context.CreateRayTracingPipeline(new()
{
    RayGeneration = rayGenShader,
    Miss = [missShader],
    AnyHit = [],
    Intersection = [],
    ClosestHit = [closestHitShader],
    HitGroups =
    [
        new HitGroup
        {
            Type = HitGroupType.Triangles,
            Name = "TriangleHitGroup",
            ClosestHit = "ClosestHit"
        }
    ],
    ResourceLayouts = [resourceLayout],
    MaxTraceRecursionDepth = 1,
    MaxPayloadSizeInBytes = 16,
    MaxAttributeSizeInBytes = 8
});
```

| Property | Description |
|----------|-------------|
| `RayGeneration` | Entry point shader |
| `Miss` | Array of miss shaders |
| `ClosestHit` | Array of closest hit shaders |
| `HitGroups` | Bundle shaders for each geometry type |
| `MaxTraceRecursionDepth` | Maximum ray bounce depth |
| `MaxPayloadSizeInBytes` | Size of data passed between shaders |

### Ray Generation Shader

```slang
[shader("raygeneration")]
void RayGen()
{
    uint2 pixelCoord = DispatchRaysIndex().xy;
    uint2 dimensions = DispatchRaysDimensions().xy;

    // Calculate ray from pixel coordinates
    float2 uv = (float2(pixelCoord) + 0.5) / float2(dimensions);
    float2 ndc = uv * 2.0 - 1.0;

    float3 rayOrigin = float3(ndc.x, -ndc.y, -1.0);
    float3 rayDir = float3(0, 0, 1);

    RayDesc ray;
    ray.Origin = rayOrigin;
    ray.Direction = rayDir;
    ray.TMin = 0.001;
    ray.TMax = 100.0;

    Payload payload;
    TraceRay(scene, RAY_FLAG_NONE, 0xFF, 0, 0, 0, ray, payload);

    outputTexture[pixelCoord] = payload.Color;
}
```

### Closest Hit Shader with Vertex Color Interpolation

```slang
[shader("closesthit")]
void ClosestHit(inout Payload payload, BuiltInTriangleIntersectionAttributes attribs)
{
    // Interpolate vertex colors using barycentric coordinates
    float3 barycentrics = float3(1.0 - attribs.barycentrics.x - attribs.barycentrics.y,
                                 attribs.barycentrics.x,
                                 attribs.barycentrics.y);

    uint primitiveIndex = PrimitiveIndex();
    uint baseIndex = primitiveIndex * 3;

    float4 color0 = vertices[baseIndex + 0].Color;
    float4 color1 = vertices[baseIndex + 1].Color;
    float4 color2 = vertices[baseIndex + 2].Color;

    payload.Color = color0 * barycentrics.x + color1 * barycentrics.y + color2 * barycentrics.z;
}
```

The closest hit shader:
1. Gets barycentric coordinates from `BuiltInTriangleIntersectionAttributes`
2. Uses `PrimitiveIndex()` to find which triangle was hit
3. Reads vertex colors from the `StructuredBuffer`
4. Interpolates colors using barycentric weights

### Dispatching Rays and Displaying Result

```csharp
// Ray tracing pass
commandBuffer.SetPipeline(pipeline);
commandBuffer.SetResourceSet(resourceSet, 0);
commandBuffer.DispatchRays(App.Width, App.Height, 1);

// Display pass
commandBuffer.BeginRenderPass(App.SwapChain.FrameBuffer, new()
{
    ColorValues = [new(0, 0, 0, 1)],
    Depth = 1.0f,
    Stencil = 0,
    Flags = ClearFlags.All
}, displayResourceSet);

commandBuffer.SetPipeline(displayPipeline);
commandBuffer.SetResourceSet(displayResourceSet, 0);
commandBuffer.SetVertexBuffer(quadVertexBuffer, 0, 0);
commandBuffer.SetIndexBuffer(quadIndexBuffer, 0, IndexFormat.UInt32);
commandBuffer.DrawIndexed(6, 1, 0, 0, 0);

commandBuffer.EndRenderPass();
```

`DispatchRays(width, height, depth)` launches the ray generation shader for each pixel, then we display the result using a fullscreen quad.

## Next Steps

Now that you understand the basics of ray tracing, you can explore:

- Adding shadows with shadow rays
- Reflections with recursive ray tracing
- Global illumination with path tracing
- [Mesh Shader](mesh-shader.md) - Another advanced GPU feature for geometry processing

For a complete deferred renderer with ray traced global illumination, check out the [SponzaScene](https://github.com/qian-o/Zenith.NET/tree/master/sources/Experiments/SponzaScene) sample.

## Source Code

> [!TIP]
> View the complete source code on GitHub: [RayTracingRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs)