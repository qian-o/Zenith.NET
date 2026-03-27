# CornellBox Development Conventions

## Overview

A dual-mode Cornell Box renderer built on the Zenith.NET multi-backend GPU framework (DirectX12 / Vulkan / Metal).
Two rendering modes switchable via ImGui radio buttons at runtime:

- **Path Tracing** (mode 0): `ComputePipeline` + inline `RayQuery<>`, progressive accumulation with NEE (Next Event Estimation), cosine-weighted hemisphere sampling, Russian roulette. Only available when `Context.Capabilities.RayTracingSupported` is true.
- **Rasterization** (mode 1): `GraphicsPipeline` + Blinn-Phong lighting, point light at ceiling, Reinhard tonemapping for emissive surfaces. Always available as fallback.

## Current State (2026-03-27)

- All files implemented and compiling successfully
- DX12 validation layer clean (added empty clear pass before rendering to initialize swap chain subresources)
- Camera initial position: (278, 273, -800), Speed=240, FarPlane=2000, looks into the box
- Swap chain format: B8G8R8A8UNorm color + D32FloatS8UInt depth/stencil
- `IRenderer` interface unifies both renderers (`Update` / `Render` / `Resize` + `IDisposable`)
- `activeRenderer` field in App.cs dispatches calls polymorphically

### Render Loop (App.cs)

1. `imGui.Update()` → `camera.Update()` → `activeRenderer.Update(camera)`
2. ImGui window: backend info, render mode radio buttons, SPP counter (path tracing only), FPS
3. Create `CommandBuffer`
4. **Empty clear pass** (`BeginRenderPass` + `EndRenderPass` with `ClearValues.Default`) to initialize swap chain
5. `activeRenderer.Render(commandBuffer, frameBuffer)`
6. `imGui.Render(commandBuffer, frameBuffer, ClearValues.None)` — no clear, overlays on top
7. `commandBuffer.Submit(true)` → `swapChain.Present()`

### Disposal Order

`pathTracer` → `rasterizer` → `imGui` → `swapChain` → `input` → `window` → `Context`

## Project Structure

```
CornellBox/
├── App.cs                              # Lifecycle, ImGui mode switching, activeRenderer dispatch
├── Program.cs                          # Entry point
├── CONVENTIONS.md                      # This file
├── Renderers/
│   ├── IRenderer.cs                    # Interface: Update / Render / Resize + IDisposable
│   ├── PathTracingRenderer.cs          # ComputePipeline + RayQuery path tracing (NEE)
│   └── RasterizationRenderer.cs        # GraphicsPipeline + Blinn-Phong
├── Handlers/
│   ├── CameraHandler.cs               # 6DOF camera (WASD+QE, right-click mouselook)
│   └── ImGuiHandler.cs                # ImGui integration (input forwarding, font loading)
├── Helpers/
│   ├── BindingHelper.cs               # Multi-backend resource binding index assignment
│   ├── CornellBoxGeometry.cs          # Shared geometry factory (PackedVertex, Material)
│   ├── CocoaHelper.cs                 # macOS Metal layer creation
│   └── Extensions.cs                  # ImGui.Overlay extension
└── Assets/
    └── Fonts/msyh.ttf                 # Chinese font for ImGui
```

## GPU Alignment Rules

### Slang Side

- **Never** use bare `float3` in ConstantBuffer / StructuredBuffer structs
- Pack `float3 + float` into `float4`, access via `property`
- Pad trailing bytes with `private float paddingN` to reach 16-byte boundary
- Attributes go **above** properties, blank line between fields

```slang
struct Material
{
    private float4 AlbedoAndEmission;

    [__unsafeForceInlineEarly]
    property float3 Albedo { get { return AlbedoAndEmission.xyz; } }

    [__unsafeForceInlineEarly]
    property float Emission { get { return AlbedoAndEmission.w; } }
};

struct CameraParams
{
    float4x4 InvView;

    float4x4 InvProjection;

    float4 PositionAndPad;

    uint FrameCount;

    uint Width;

    uint Height;

    private float padding0;

    property float3 Position { get { return PositionAndPad.xyz; } }
};
```

### C# Side

- Use `LayoutKind.Explicit` + `FieldOffset` for precise offset control
- Specify `Size` to ensure total size matches Slang side
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
    public Vector4 PositionAndPad;

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

### Vertex Input Exception

Vertex buffers use `InputLayout` + `ElementFormat` for precise layout, so `float3` **is allowed**:

```csharp
[StructLayout(LayoutKind.Sequential)]
file struct Vertex
{
    public Vector3 Position;

    public Vector3 Normal;

    public uint MaterialID;

    public float Padding;
}
```

Corresponding shader vertex input uses semantic annotations:

```slang
struct VSInput
{
    float3 Position : POSITION0;

    float3 Normal : NORMAL0;

    uint MatID : TEXCOORD0;
};
```

`InputLayout` must match:

```csharp
InputLayout inputLayout = new();
inputLayout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
inputLayout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Normal });
inputLayout.Add(new() { Format = ElementFormat.UInt1, Semantic = ElementSemantic.TexCoord });
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
            InstanceID = 0,
            InstanceMask = 0xFF,
            Transform = Matrix4x4.Identity,
            Flags = RayTracingInstanceFlags.None
        },
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
    new() { Type = ResourceType.StructuredBuffer, ... },       // [2] materials
);

// Shader declares in same order:
// RaytracingAccelerationStructure scene;
// ConstantBuffer<CameraParams> camera;
// StructuredBuffer<Material> materials;

// ResourceTable passes in same order
resourceTable = App.Context.CreateResourceTable(new()
{
    Layout = resourceLayout,
    Resources = [tlas, cameraBuffer, materialBuffer]
});
```

`BindingHelper.Bindings()` handles per-backend index differences transparently.

## Render Loop Patterns

### Compute Output → SwapChain

```csharp
cmd.SetPipeline(computePipeline);
cmd.SetResourceTable(resourceTable);
cmd.Dispatch(dispatchX, dispatchY, 1);

Texture colorTarget = App.SwapChain.FrameBuffer.Desc.ColorAttachments[0].Target;
cmd.CopyTexture(outputTexture, default, default,
                colorTarget, default, default,
                new() { Width = w, Height = h, Depth = 1 });
```

### Graphics RenderPass → SwapChain

```csharp
cmd.BeginRenderPass(App.SwapChain.FrameBuffer, new()
{
    ColorValues = [new(0, 0, 0, 1)],
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

- Texture (outputTexture, accumulationTexture)
- ResourceTable (references Texture)
- Reset path tracing frameCount to 0

No rebuild needed: Buffer, Pipeline, ResourceLayout, acceleration structures.

```csharp
public void Resize(uint width, uint height)
{
    resourceTable?.Dispose();
    resourceTable = null;
    outputTexture?.Dispose();
    outputTexture = null;
    accumulationTexture?.Dispose();
    accumulationTexture = null;
    // Lazy rebuild on next Render call
}
```

## Dispose Order

Release in reverse creation order — downstream first, upstream last:

```
ResourceTable → Pipeline → ResourceLayout
→ Texture (output / accumulation)
→ TLAS → BLAS[]
→ Buffer (camera / material / index / vertex)
```

## Cornell Box Geometry Data (CornellBoxGeometry.cs)

- Coordinate range 0~560 (standard Cornell Box specification)
- 16 quads (64 vertices, 96 indices): 5 walls + 5 short block faces + 5 tall block faces + 1 light
- 4 material groups: 0=red left wall, 1=green right wall, 2=white surfaces (ceiling/floor/back wall/two blocks), 3=light
- Each quad → 4 vertices + 6 indices (2 triangles)
- Normals auto-computed via `normalize(cross(v1-v0, v2-v0))`
- Material ID packed into vertex as `BitConverter.UInt32BitsToSingle(materialID)`, decoded in shader via `asuint()`
- Material colors: red(0.63,0.06,0.06), green(0.14,0.45,0.09), white(0.73,0.71,0.68), light(1.0,0.85,0.6)+emission=15

### Shared Data Types

```csharp
// 32 bytes — used by both renderers
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct PackedVertex
{
    [FieldOffset(0)]
    public Vector4 PositionAndMatID;    // xyz=position, w=asuint(materialID)

    [FieldOffset(16)]
    public Vector4 NormalAndPad;        // xyz=normal, w=0
}

// 16 bytes
[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct Material
{
    [FieldOffset(0)]
    public Vector4 AlbedoAndEmission;   // xyz=albedo, w=emission strength (0 or 15)
}
```

## IRenderer Interface

```csharp
internal interface IRenderer : IDisposable
{
    void Update(CameraHandler camera);
    void Render(CommandBuffer commandBuffer, FrameBuffer frameBuffer);
    void Resize(uint width, uint height);
}
```

## PathTracingRenderer Details

- **Pipeline**: `ComputePipeline` with `[numthreads(16,16,1)]`
- **Shader**: Inline Slang string, entry point `CSMain`
- **Algorithm**: 5-bounce path tracing + NEE (Next Event Estimation) + Russian roulette (bounce ≥ 2)
- **Accumulation**: R32G32B32A32Float texture, progressive average with jittered subpixel sampling
- **Output**: Gamma-corrected to B8G8R8A8UNorm, then `CopyTexture` to swap chain color target
- **Camera change detection**: Compares `View` / `Projection` matrices, resets `frameCount` on change
- **Light sampling**: Hardcoded ceiling light quad (213–343, 548.6, 227–332), area = 13650
- **Resource bindings** (7): AccelStruct, ConstantBuffer, 3× StructuredBuffer (vertices/indices/materials), 2× TextureReadWrite (accum/output)
- `ResetAccumulation()`: Resets `frameCount` to 0 when switching mode
- `Resize()`: Disposes textures + resource table, lazy rebuilt on next `Render()`

## RasterizationRenderer Details

- **Pipeline**: `GraphicsPipeline` with Vertex + Pixel shaders
- **Shader**: Inline Slang string, entry points `VSMain` / `PSMain`
- **Algorithm**: Blinn-Phong with ambient(0.08) + diffuse + specular(pow 64, 0.1 weight) + distance attenuation
- **Light**: Point light at (278, 548, 280), color (2.0, 1.8, 1.4)
- **Emissive**: Reinhard tonemapping `color / (color + 1)` for light quad
- **Rasterizer**: `CullNone` (camera is inside the box)
- **Material lookup**: `nointerpolation uint MaterialID : TEXCOORD2` — flat interpolation, extracted via `asuint(PositionAndMatID.w)` in vertex shader
- **InputLayout**: Float4 (POSITION) + Float4 (NORMAL), matches PackedVertex stride=32
- **Resource bindings** (2): ConstantBuffer (RasterConstants 240B), StructuredBuffer (Materials)
- `Resize()`: No-op (no size-dependent resources)

## DX12 Specific Notes

- Swap chain subresources must be initialized before use → empty clear pass `BeginRenderPass(fb, ClearValues.Default)` + `EndRenderPass()` before rendering
- `BindingHelper` assigns DX12 register indices by type: CBV(b), SRV(t), UAV(u), Sampler(s) independently numbered
- Vulkan numbers all bindings sequentially; Metal uses argument buffer index

## C# Code Style

- Prefer `is` / `is not` pattern matching over `==` / `!=` for comparisons
- Use full `using` imports, short type names in code (no `Namespace.Type` inline references)
- `file struct` for shader-mirrored GPU structs (scoped to the file that uses them)
- Shaders inlined as `const string ShaderSource = """...""";` (raw string literal)
- Collection expressions: `[]` for empty, `[.. spread]` for conversion
- Target-typed `new()` for object initializers
- `static readonly` fields for framework objects created once
- Blank line between each field / property / method
