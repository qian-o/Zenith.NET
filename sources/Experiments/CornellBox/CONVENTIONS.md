# CornellBox Development Conventions

## Overview

A dual-mode Cornell Box renderer built on the Zenith.NET multi-backend GPU framework (DirectX12 / Metal / Vulkan).
Two rendering modes switchable via ImGui radio buttons at runtime:

- **Path Tracing** (mode 0): `ComputePipeline` + inline `RayQuery<>`, progressive accumulation with NEE (Next Event Estimation), Cook-Torrance PBR BRDF (GGX + Schlick Fresnel + Smith G), GGX importance sampling for specular, cosine-weighted hemisphere for diffuse, Russian roulette, environment sky light on ray miss. Only available when `Context.Capabilities.RayTracingSupported` is true.
- **Rasterization** (mode 1): `GraphicsPipeline` + Blinn-Phong lighting, point light at ceiling, hemisphere ambient, ACES tonemapping. Always available as fallback.

## Current State (2026-03-30)

- All files implemented and compiling successfully
- DirectX12 validation layer clean
- Camera initial position: (278, 273, -800), Speed=240, FarPlane=2000, looks into the box
- Swap chain format: B8G8R8A8UNorm color + D32FloatS8UInt depth/stencil
- `Renderer` abstract base class unifies both renderers (`Update` / `Render` / `Resize` + `IDisposable`)
- `activeRenderer` field in App.cs dispatches calls polymorphically

### Render Loop (App.cs)

1. `imGui.Update()` → `camera.Update()` → `activeRenderer.Update(camera)`
2. ImGui window: backend info, render mode radio buttons, SPP counter (path tracing only), FPS
3. Create `CommandBuffer`
4. `imGui.Render(commandBuffer, swapChain.FrameBuffer, ClearValues.Default)` — ImGui clears swap chain and renders UI overlay
5. `activeRenderer.Render(commandBuffer)` — renderer writes to its own Color texture, displayed via ImGui `AddImage`
6. `commandBuffer.Submit(true)` → `swapChain.Present()`

### Disposal Order

`pathTracer` → `rasterizer` → `imGui` → `swapChain` → `input` → `window` → `Context`

## Project Structure

```
CornellBox/
├── App.cs                              # Lifecycle, ImGui mode switching, activeRenderer dispatch
├── Program.cs                          # Entry point
├── CONVENTIONS.md                      # This file
├── Renderers/
│   ├── Renderer.cs                     # Abstract base class: Color / DepthStencil / FrameBuffer management
│   ├── PathTracingRenderer.cs          # ComputePipeline + RayQuery path tracing (NEE + PBR BRDF)
│   └── RasterizationRenderer.cs        # GraphicsPipeline + Blinn-Phong
├── Handlers/
│   ├── CameraHandler.cs               # 6DOF camera (WASD+QE, right-click mouselook)
│   └── ImGuiHandler.cs                # ImGui integration (input forwarding, font loading)
├── Helpers/
│   ├── BindingHelper.cs               # Multi-backend resource binding index assignment
│   ├── CornellBoxGeometry.cs          # Shared geometry factory (Vertex, Material)
│   ├── CocoaHelper.cs                 # macOS Metal layer creation
│   └── Extensions.cs                  # ImGui.Overlay extension
└── Assets/
    └── Fonts/msyh.ttf                 # Chinese font for ImGui
```

## GPU Alignment Rules

### Slang Side

- **Never** use bare `float3` in ConstantBuffer / StructuredBuffer structs
- Pack `float3` into `float4`, use `private` field + `property` accessor
- If the next field after `float3` is a scalar (`float` / `uint`), merge them into one `float4` with a meaningful combined name (e.g. `NormalAndMaterialID`, `AlbedoAndEmission`); if there is no natural scalar to pair, use `XXXAndPadding`
- **Only** `float3` needs the `float4` + property pattern; scalar types (`float`, `uint`, `int`, `float4x4`, etc.) can be declared directly as normal fields
- Pad trailing bytes with `private float paddingN` to reach 16-byte boundary
- Vertex I/O structs (VSInput) also use `private float4` + `property` for consistency — semantic annotations go on the backing field
- PSInput interpolators can use `float3` directly (controlled by semantic output)
- Attributes go **above** properties, blank line between fields

```slang
struct Vertex
{
    private float4 PositionAndPadding;

    private float4 NormalAndMaterialID;

    property float3 Position { get { return PositionAndPadding.xyz; } }

    property float3 Normal { get { return NormalAndMaterialID.xyz; } }

    property uint MaterialID { get { return asuint(NormalAndMaterialID.w); } }
};

struct Material
{
    private float4 AlbedoAndEmission;

    float Metallic;

    float Roughness;

    private float padding0;

    private float padding1;

    property float3 Albedo { get { return AlbedoAndEmission.xyz; } }

    property float Emission { get { return AlbedoAndEmission.w; } }
};

struct CameraParams
{
    float4x4 InvView;

    float4x4 InvProjection;

    private float4 PositionAndPadding;

    uint FrameCount;

    uint Width;

    uint Height;

    private float padding0;

    property float3 Position { get { return PositionAndPadding.xyz; } }
};
```

### C# Side

- Use `LayoutKind.Explicit` + `FieldOffset` for precise offset control
- Specify `Size` to ensure total size matches Slang side
- C# structs use split, human-readable fields (`Position`, `Normal`, `MaterialID`) — the GPU-side packing is handled by `FieldOffset` matching the Slang `float4` layout
- ConstantBuffer requires 256-byte alignment (`BufferUsageFlags.Constant`)
- Attributes go **above** the field, blank line between fields

```csharp
[StructLayout(LayoutKind.Explicit, Size = 160)]
file struct CameraParams
{
    [FieldOffset(0)]
    public Matrix4x4 InvView;

    [FieldOffset(64)]
    public Matrix4x4 InvProjection;

    [FieldOffset(128)]
    public Vector3 Position;

    [FieldOffset(144)]
    public uint FrameCount;

    [FieldOffset(148)]
    public uint Width;

    [FieldOffset(152)]
    public uint Height;
}
```

### Alignment Quick Reference

| Type | Size | Alignment | Notes |
|------|------|-----------|-------|
| `float` / `uint` / `int` | 4B | 4B | |
| `float2` / `uint2` | 8B | 8B | |
| `float4` / `uint4` | 16B | 16B | Use instead of float3 |
| `float4x4` | 64B | 16B | `Matrix4x4` |
| struct | - | 16B boundary | Total size must be multiple of 16 |

### Vertex Input Layout

Vertex buffers use `InputLayout` + `ElementFormat`. The C# struct uses split fields with `FieldOffset`, while Slang uses `private float4` + `property` for both StructuredBuffer and vertex input:

```csharp
// 32 bytes — used by both renderers
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct Vertex
{
    [FieldOffset(0)]
    public Vector3 Position;     // maps to PositionAndPadding.xyz

    [FieldOffset(16)]
    public Vector3 Normal;       // maps to NormalAndMaterialID.xyz

    [FieldOffset(28)]
    public uint MaterialID;      // maps to NormalAndMaterialID.w
}
```

Slang StructuredBuffer struct (path tracing):

```slang
struct Vertex
{
    private float4 PositionAndPadding;

    private float4 NormalAndMaterialID;

    property float3 Position { get { return PositionAndPadding.xyz; } }

    property float3 Normal { get { return NormalAndMaterialID.xyz; } }

    property uint MaterialID { get { return asuint(NormalAndMaterialID.w); } }
};
```

Slang vertex input struct (rasterization) — same layout with semantic annotations:

```slang
struct VSInput
{
    private float4 PositionAndPadding : POSITION0;

    private float4 NormalAndMaterialID : NORMAL0;

    property float3 Position { get { return PositionAndPadding.xyz; } }

    property float3 Normal { get { return NormalAndMaterialID.xyz; } }

    property uint MaterialID { get { return asuint(NormalAndMaterialID.w); } }
};
```

`InputLayout` matches the `float4 + float4` backing fields:

```csharp
InputLayout inputLayout = new();
inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Position });
inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Normal });
```

## Resource Creation Patterns

### Buffer

```csharp
// Vertex buffer (AccelerationStructure flag needed for ray tracing)
buffer = App.Context.CreateBuffer(new()
{
    SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length),
    StrideInBytes = (uint)sizeof(Vertex),
    Flags = BufferUsageFlags.Vertex | BufferUsageFlags.AccelerationStructure
});
buffer.Upload(vertices, 0);

// ConstantBuffer (256B aligned, MapWrite for per-frame update)
cbuffer = App.Context.CreateBuffer(new()
{
    SizeInBytes = (uint)sizeof(CameraParams),
    StrideInBytes = (uint)sizeof(CameraParams),
    Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
});

// StructuredBuffer (read-only)
sbuffer = App.Context.CreateBuffer(new()
{
    SizeInBytes = (uint)(sizeof(Material) * count),
    StrideInBytes = (uint)sizeof(Material),
    Flags = BufferUsageFlags.ShaderResource
});
```

### Texture (UAV / Accumulation Buffer)

```csharp
texture = App.Context.CreateTexture(new()
{
    Type = TextureType.Texture2D,
    Format = PixelFormat.R32G32B32A32Float,
    Width = width, Height = height, Depth = 1,
    MipLevels = 1, ArrayLayers = 1,
    SampleCount = SampleCount.Count1,
    Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
});
```

## Acceleration Structure Build Pattern

```csharp
CommandBuffer buildCmd = App.Context.Graphics.CommandBuffer();

// BLAS — one per geometry group
blas = buildCmd.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
{
    Geometries = [new()
    {
        Type = RayTracingGeometryType.Triangles,
        Triangles = new()
        {
            VertexBuffer = vertexBuffer,
            VertexFormat = PixelFormat.R32G32B32Float,
            VertexCount = vertexCount,
            VertexStrideInBytes = (uint)sizeof(Vertex),
            IndexBuffer = indexBuffer,
            IndexFormat = IndexFormat.UInt32,
            IndexCount = indexCount,
            Transform = Matrix4x4.Identity
        },
        Flags = RayTracingGeometryFlags.Opaque
    }],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});

// TLAS — references all BLAS instances
tlas = buildCmd.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
{
    Instances = [
        new()
        {
            AccelerationStructure = blas,
            ID = 0,
            Mask = 0xFF,
            Transform = Matrix4x4.Identity,
            Flags = RayTracingInstanceFlags.None
        }
    ],
    Flags = AccelerationStructureBuildFlags.PreferFastTrace
});

buildCmd.Submit(waitForCompletion: true);
```

## Pipeline Creation Patterns

### ComputePipeline (Path Tracing)

```csharp
resourceLayout = App.Context.CreateResourceLayout(new()
{
    Bindings = BindingHelper.Bindings(
        new() { Type = ResourceType.AccelerationStructure, Count = 1, StageFlags = ShaderStageFlags.Compute },
        new() { Type = ResourceType.ConstantBuffer,        Count = 1, StageFlags = ShaderStageFlags.Compute },
        new() { Type = ResourceType.StructuredBuffer,      Count = 1, StageFlags = ShaderStageFlags.Compute },
        new() { Type = ResourceType.StructuredBuffer,      Count = 1, StageFlags = ShaderStageFlags.Compute },
        new() { Type = ResourceType.StructuredBuffer,      Count = 1, StageFlags = ShaderStageFlags.Compute },
        new() { Type = ResourceType.TextureReadWrite,      Count = 1, StageFlags = ShaderStageFlags.Compute },
        new() { Type = ResourceType.TextureReadWrite,      Count = 1, StageFlags = ShaderStageFlags.Compute }
    )
});

using Shader cs = App.Context.LoadShaderFromSource(ShaderSource, "CSMain", ShaderStageFlags.Compute);
pipeline = App.Context.CreateComputePipeline(new()
{
    Compute = cs,
    ResourceLayout = resourceLayout,
    ThreadGroupSizeX = 16, ThreadGroupSizeY = 16, ThreadGroupSizeZ = 1
});
```

### GraphicsPipeline (Rasterization)

```csharp
using Shader vs = App.Context.LoadShaderFromSource(ShaderSource, "VSMain", ShaderStageFlags.Vertex);
using Shader ps = App.Context.LoadShaderFromSource(ShaderSource, "PSMain", ShaderStageFlags.Pixel);

pipeline = App.Context.CreateGraphicsPipeline(new()
{
    RenderStates = new()
    {
        RasterizerState = RasterizerStates.CullBack,
        DepthStencilState = DepthStencilStates.Default,
        BlendState = BlendStates.Opaque
    },
    Vertex = vs, Pixel = ps,
    ResourceLayout = resourceLayout,
    InputLayouts = [inputLayout],
    PrimitiveTopology = PrimitiveTopology.TriangleList,
    Output = App.SwapChain.FrameBuffer.Output
});
```

## Resource Binding Pattern

Declaration order in ResourceLayout Bindings = declaration order in shader = resource order in ResourceTable.

```csharp
// Layout declaration order
Bindings = BindingHelper.Bindings(
    new() { Type = ResourceType.AccelerationStructure, ... },  // [0] scene
    new() { Type = ResourceType.ConstantBuffer, ... },         // [1] camera
    new() { Type = ResourceType.StructuredBuffer, ... },       // [2] vertices
    new() { Type = ResourceType.StructuredBuffer, ... },       // [3] indices
    new() { Type = ResourceType.StructuredBuffer, ... },       // [4] materials
    new() { Type = ResourceType.TextureReadWrite, ... },       // [5] accumTexture
    new() { Type = ResourceType.TextureReadWrite, ... },       // [6] outputTexture
);

// Shader declares in same order:
// RaytracingAccelerationStructure scene;
// ConstantBuffer<CameraParams> camera;
// StructuredBuffer<Vertex> vertices;
// StructuredBuffer<uint> indices;
// StructuredBuffer<Material> materials;
// RWTexture2D<float4> accumTexture;
// RWTexture2D<float4> outputTexture;

// ResourceTable passes in same order
resourceTable = App.Context.CreateResourceTable(new()
{
    Layout = resourceLayout,
    Resources = [tlas, cameraBuffer, vertexBuffer, indexBuffer, materialBuffer, accumTexture, Color]
});
```

`BindingHelper.Bindings()` handles per-backend index differences transparently.

## Render Loop Patterns

### Compute Output → Color Texture

```csharp
cmd.SetPipeline(computePipeline);
cmd.SetResourceTable(resourceTable);
cmd.Dispatch(dispatchX, dispatchY, 1);
```

The compute shader writes directly to the base class `Color` texture (bound as `outputTexture` UAV in the ResourceTable). No `CopyTexture` needed — ImGui displays it via `AddImage`.

### Graphics RenderPass → SwapChain

```csharp
cmd.BeginRenderPass(App.SwapChain.FrameBuffer, new()
{
    ColorValues = [new(0.51f, 0.518f, 0.557f, 1)],
    Depth = 1.0f, Stencil = 0,
    Flags = ClearFlags.All
});
cmd.SetPipeline(graphicsPipeline);
cmd.SetResourceTable(resourceTable);
cmd.SetVertexBuffer(vertexBuffer, 0, 0);
cmd.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
cmd.DrawIndexed(indexCount, 1, 0, 0, 0);
cmd.EndRenderPass();
```

## Resize Pattern

Resources to rebuild on size change:

- Texture (accumulationTexture)
- ResourceTable (references Texture)
- Reset path tracing FrameCount to 0

No rebuild needed: Buffer, Pipeline, ResourceLayout, acceleration structures.

```csharp
public void Resize(uint width, uint height)
{
    base.Resize(width, height);  // recreates Color + DepthStencil + FrameBuffer

    resourceTable?.Dispose();
    resourceTable = null;
    accumulationTexture?.Dispose();
    accumulationTexture = null;

    FrameCount = 0;
    // Lazy rebuild on next Render call
}
```

## Dispose Order

Release in reverse creation order — downstream first, upstream last:

```
ResourceTable → Pipeline → ResourceLayout
→ Texture (accumulation)
→ TLAS → BLAS[]
→ Buffer (camera / material / index / vertex)
```

## Cornell Box Geometry Data (CornellBoxGeometry.cs)

- Coordinate range 0~560 (standard Cornell Box specification)
- 16 quads (64 vertices, 96 indices): 5 walls + 5 short block faces + 5 tall block faces + 1 light
- 6 material groups: 0=red left wall, 1=green right wall, 2=white surfaces (ceiling/floor/back wall), 3=light, 4=short block (smooth diffuse), 5=tall block (metallic mirror)
- Each quad → 4 vertices + 6 indices (2 triangles)
- Normals auto-computed via `normalize(cross(v1-v0, v2-v0))`
- Material ID stored in `Vertex.MaterialID` (C#), packed into `NormalAndMaterialID.w` on GPU via `FieldOffset(28)` overlapping the `float4` w-component
- Material colors: red(0.63,0.06,0.06), green(0.14,0.45,0.09), white(0.73,0.71,0.68), light(1.0,0.85,0.6)+emission=25, short block(0.73,0.71,0.68)+roughness=0.3, tall block(0.95,0.93,0.88)+metallic=1.0+roughness=0.05

### Shared Data Types

```csharp
// 32 bytes — used by both renderers
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct Vertex
{
    [FieldOffset(0)]
    public Vector3 Position;

    [FieldOffset(16)]
    public Vector3 Normal;

    [FieldOffset(28)]
    public uint MaterialID;
}

// 32 bytes — PBR material with metallic/roughness
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct Material
{
    [FieldOffset(0)]
    public Vector3 Albedo;

    [FieldOffset(12)]
    public float Emission;

    [FieldOffset(16)]
    public float Metallic;

    [FieldOffset(20)]
    public float Roughness;
}
```

## Renderer Base Class (Renderer.cs)

```csharp
internal abstract class Renderer : IDisposable
{
    public Texture Color { get; private set; }
    public Texture DepthStencil { get; private set; }
    public FrameBuffer FrameBuffer { get; private set; }

    abstract void Update(CameraHandler camera);
    abstract void Render(CommandBuffer commandBuffer);
    virtual void Resize(uint width, uint height);  // recreates Color + DepthStencil + FrameBuffer
    virtual void Dispose();                         // disposes FrameBuffer + DepthStencil + Color
}
```

- Constructor calls `Resize(App.Width, App.Height)` to create initial resources
- `Resize()`: Disposes then recreates `Color` (B8G8R8A8UNorm, RenderTarget | ShaderResource | UnorderedAccess), `DepthStencil` (D32FloatS8UInt), and `FrameBuffer`
- `Color` texture is used by path tracer as compute output target, and displayed via ImGui `AddImage` in App.cs
- Subclasses call `base.Resize()` / `base.Dispose()` to manage these shared resources

## PathTracingRenderer Details

### Overview

- **Pipeline**: `ComputePipeline` with `[numthreads(16,16,1)]`, entry point `CSMain`
- **Shader**: Inline Slang raw string literal
- **Algorithm**: 8-bounce path tracing + NEE (Next Event Estimation) + Cook-Torrance PBR BRDF + Russian roulette (bounce ≥ 2)
- **Accumulation**: R32G32B32A32Float UAV texture, progressive average with jittered subpixel sampling
- **Tonemapping**: ACES filmic + gamma correction
- **Environment**: Gradient sky light on ray miss (warm ground → cool sky)
- **Output**: Tonemapped + gamma-corrected to `Color` texture (base class, B8G8R8A8UNorm)

### Shader Structs

```slang
struct Vertex           // StructuredBuffer — private float4 + property
struct Material         // StructuredBuffer — private float4 + property
struct CameraParams     // ConstantBuffer — float4x4 × 2, private float4 Position, uint × 3 + padding
```

### Resource Bindings (7 slots)

| Index | Type | Shader Variable | Description |
|-------|------|-----------------|-------------|
| 0 | AccelerationStructure | `scene` | TLAS for ray queries |
| 1 | ConstantBuffer | `camera` | CameraParams (160B) |
| 2 | StructuredBuffer | `vertices` | Vertex[] (32B stride) |
| 3 | StructuredBuffer | `indices` | uint[] |
| 4 | StructuredBuffer | `materials` | Material[] (32B stride) |
| 5 | TextureReadWrite | `accumTexture` | R32G32B32A32Float accumulation buffer |
| 6 | TextureReadWrite | `outputTexture` | Base class `Color` texture (compute write target) |

### Shader Functions

| Function | Purpose |
|----------|---------|
| `pcgHash(uint)` | PCG hash for RNG seed |
| `randomFloat(inout uint)` | Returns [0,1) float, advances seed |
| `cosineSampleHemisphere(float3, inout uint)` | Cosine-weighted hemisphere sampling around normal |
| `traceShadowRay(float3, float3, float)` | Shadow ray test using `RayQuery<RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH>` |
| `sampleLightDirect(float3, float3, float3, Material, inout uint)` | NEE: uniform sample on ceiling light quad, Cook-Torrance BRDF, geometry term |
| `DistributionGGX(float, float)` | GGX normal distribution function |
| `GeometrySchlickGGX(float, float)` | Schlick-GGX geometry sub-function |
| `GeometrySmith(float, float, float)` | Smith geometry function (combined) |
| `FresnelSchlick(float, float3)` | Schlick Fresnel approximation |
| `evaluateBRDF(float3, float3, float3, Material)` | Full Cook-Torrance BRDF evaluation (diffuse + specular) |
| `sampleGGXHalfVector(float3, float, inout uint)` | GGX importance sampling for specular half-vector |
| `tracePath(float3, float3, inout uint)` | Main path tracing loop (8 bounces max) |
| `CSMain(uint3)` | Entry point: generate ray, trace, accumulate, ACES tonemap, gamma-correct |

### Path Tracing Algorithm (`tracePath`)

1. For each bounce (max 8):
   - Trace primary ray via `RayQuery<RAY_FLAG_NONE>`
   - On miss → add environment sky contribution (`lerp` warm ground to cool sky based on `direction.y`) → break
   - Compute hit position, interpolate normal via barycentric weights from index/vertex buffers
   - Flip normal if back-facing (`dot(normal, direction) > 0`)
   - If emissive material: accumulate emission on bounce 0 only, then break (prevents double-counting with NEE)
   - Non-emissive: add NEE contribution via `sampleLightDirect()` using Cook-Torrance BRDF
   - Probabilistic BRDF sampling: compute `specProb` from F0 and metallic, then:
     - With probability `specProb`: GGX importance sample half-vector → reflect → specular throughput (clamped to 10)
     - Otherwise: cosine-weighted hemisphere → diffuse throughput
   - Russian roulette (bounce ≥ 2): survival probability = max component of throughput
   - Per-sample radiance clamped to 30 to suppress fireflies

### Light Constants (hardcoded)

```slang
static const float3 LightMin = float3(213.0, 548.6, 227.0);
static const float3 LightMax = float3(343.0, 548.6, 332.0);
static const float LightArea = 13650.0;  // (343-213) * (332-227)
static const float3 LightNormal = float3(0.0, -1.0, 0.0);
```

### Camera Ray Generation (`CSMain`)

1. RNG seed: `pcgHash(pixel.x + pixel.y * Width + FrameCount * Width * Height)`
2. Jittered subpixel offset → UV → NDC (y-flipped)
3. `InvProjection` → local direction → `InvView` → world direction
4. Origin = `camera.Position`

### Accumulation

- `FrameCount == 0`: overwrite `accumTexture` with new sample
- `FrameCount > 0`: add to existing accumulation
- Running average: `accumulated.rgb / (FrameCount + 1)`
- ACES tonemapping: `saturate((x * (2.51x + 0.03)) / (x * (2.43x + 0.59) + 0.14))`
- Gamma correction: `pow(avg, 1/2.2)` → `outputTexture`

### Camera Change Detection

```csharp
if (view != lastView || projection != lastProjection)
{
    lastView = view;
    lastProjection = projection;
    FrameCount = 0;  // reset accumulation
}
```

### C# Side CameraParams (160B)

```csharp
[StructLayout(LayoutKind.Explicit, Size = 160)]
file struct CameraParams
{
    [FieldOffset(0)]   public Matrix4x4 InvView;
    [FieldOffset(64)]  public Matrix4x4 InvProjection;
    [FieldOffset(128)] public Vector3 Position;
    [FieldOffset(144)] public uint FrameCount;
    [FieldOffset(148)] public uint Width;
    [FieldOffset(152)] public uint Height;
}
```

### Buffer Setup

- `vertexBuffer`: ShaderResource | AccelerationStructure
- `indexBuffer`: ShaderResource | AccelerationStructure
- `materialBuffer`: ShaderResource
- `cameraBuffer`: Constant | MapWrite (per-frame upload)
- Acceleration structures: single BLAS (all geometry, PreferFastTrace) → single TLAS (one instance)

### Resize / Lifecycle

- `Resize()`: calls `base.Resize()`, disposes `resourceTable` + `accumulationTexture`, set to null for lazy rebuild
- `Render()`: if resources are null → create `accumulationTexture` (R32G32B32A32Float) + `resourceTable`, reset `FrameCount`
- `Dispatch()`: `ceil(Width/16) × ceil(Height/16) × 1`
- `FrameCount++` after each dispatch

### Dispose Order

```
base (FrameBuffer + DepthStencil + Color)
→ resourceTable → accumulationTexture
→ pipeline → resourceLayout
→ tlas → blas
→ cameraBuffer → materialBuffer → indexBuffer → vertexBuffer
```

## RasterizationRenderer Details

### Overview

- **Pipeline**: `GraphicsPipeline` with Vertex + Pixel shaders, entry points `VSMain` / `PSMain`
- **Shader**: Inline Slang raw string literal
- **Algorithm**: Blinn-Phong shading with point light, hemisphere ambient, ACES tonemapping
- **Output**: Renders directly to `FrameBuffer` (base class) via render pass

### Shader Structs

```slang
struct Material         // StructuredBuffer — private float4 + property
struct RasterConstants  // ConstantBuffer — float4x4 × 3, private float4 × 3 + properties
struct VSInput          // Vertex input — private float4 × 2 + properties (Position, Normal, MaterialID)
struct PSInput          // Interpolated — float4 Position, float3 WorldPos, float3 Normal, nointerpolation uint MaterialID
```

### Resource Bindings (2 slots)

| Index | Type | Shader Variable | Stages | Description |
|-------|------|-----------------|--------|-------------|
| 0 | ConstantBuffer | `cb` | Vertex + Pixel | RasterConstants (240B) |
| 1 | StructuredBuffer | `materials` | Pixel | Material[] (32B stride) |

### Vertex Shader (`VSMain`)

1. Transform position: `worldPos = mul(float4(input.Position, 1.0), cb.Model)`
2. Clip space: `mul(mul(worldPos, cb.View), cb.Projection)`
3. Pass world position, transformed normal, MaterialID to PSInput
4. MaterialID via `asuint(NormalAndMaterialID.w)` through VSInput property, passed as `nointerpolation uint`

### Pixel Shader (`PSMain`)

1. **Emissive check**: if `mat.Emission > 0` → Reinhard tonemapping: `color / (color + 1)` → gamma correct → return
2. **Blinn-Phong**:
   - Hemisphere ambient: `albedo * lerp(0.06, 0.15, N.y * 0.5 + 0.5)` (brighter on upward-facing surfaces)
   - Diffuse: `albedo * lightColor * NdotL * atten`
   - Specular: `lightColor * pow(NdotH, 64) * atten * 0.1`
   - Distance attenuation: `1 / (1 + 0.000005 * dist²)`
3. ACES tonemapping: `saturate((x * (2.51x + 0.03)) / (x * (2.43x + 0.59) + 0.14))`
4. Gamma correction: `pow(color, 1/2.2)`

### Light Configuration

- Point light position: (278, 548, 280)
- Light color: (2.0, 1.8, 1.4)
- Ambient factor: hemisphere-based (0.06 bottom to 0.15 top)
- Specular exponent: 64, weight: 0.1

### C# Side RasterConstants (240B)

```csharp
[StructLayout(LayoutKind.Explicit, Size = 240)]
file struct RasterConstants
{
    [FieldOffset(0)]   public Matrix4x4 Model;       // Identity
    [FieldOffset(64)]  public Matrix4x4 View;
    [FieldOffset(128)] public Matrix4x4 Projection;
    [FieldOffset(192)] public Vector3 LightPos;       // maps to private float4 LightPosAndPadding
    [FieldOffset(208)] public Vector3 LightColor;     // maps to private float4 LightColorAndPadding
    [FieldOffset(224)] public Vector3 CameraPos;      // maps to private float4 CameraPosAndPadding
}
```

### Pipeline Configuration

- RasterizerState: `CullNone` (camera is inside the box)
- DepthStencilState: `Default`
- BlendState: `Opaque`
- PrimitiveTopology: `TriangleList`
- InputLayout: Float4 (POSITION) + Float4 (NORMAL), stride = 32

### Buffer Setup

- `vertexBuffer`: Vertex only
- `indexBuffer`: Index only
- `materialBuffer`: ShaderResource
- `constantBuffer`: Constant | MapWrite (per-frame upload)
- `resourceTable`: created once in constructor (no size-dependent resources)

### Render Pass

```csharp
cmd.BeginRenderPass(FrameBuffer, clearValues, resourceTable);
cmd.SetPipeline(pipeline);
cmd.SetResourceTable(resourceTable);
cmd.SetVertexBuffer(vertexBuffer, 0, 0);
cmd.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
cmd.DrawIndexed(indexCount, 1, 0, 0, 0);
cmd.EndRenderPass();
```

Clear values: color (0.51, 0.518, 0.557, 1) matching environment sky, depth 1.0, stencil 0, ClearFlags.All

### Resize / Lifecycle

- `Resize()`: only `base.Resize()` — no renderer-specific size-dependent resources
- No `accumulationTexture`, no `resourceTable` rebuild needed

### Dispose Order

```
base (FrameBuffer + DepthStencil + Color)
→ pipeline → resourceTable → resourceLayout
→ constantBuffer → materialBuffer → indexBuffer → vertexBuffer
```

## DirectX12 Specific Notes

- `BindingHelper` assigns DirectX12 register indices by type: CBV(b), SRV(t), UAV(u), Sampler(s) independently numbered
- Metal uses argument buffer index; Vulkan numbers all bindings sequentially

## C# Code Style

- Prefer `is` / `is not` pattern matching over `==` / `!=` for comparisons
- Use full `using` imports, short type names in code (no `Namespace.Type` inline references)
- `file struct` for shader-mirrored GPU structs (scoped to the file that uses them)
- Shaders inlined as `const string ShaderSource = """...""";` (raw string literal)
- Collection expressions: `[]` for empty, `[.. spread]` for conversion
- Target-typed `new()` for object initializers
- `static readonly` fields for framework objects created once
- Blank line between each field / property / method
