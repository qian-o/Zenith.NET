# Fix all GPU struct layouts for cross-backend compatibility (DX12 / Vulkan / Metal)

## Background

Slang compiles shaders to DXIL, SPIR-V, and metallib. Each backend applies different
alignment and packing rules to struct members containing `float2`, `float3`, or mixed
scalars. This affects both `ConstantBuffer<T>` (std140 / cbuffer / MSL rules) and
`StructuredBuffer<T>` (std430 vs scalar layout — `vec3` aligns to 16 in std430 but 4
in scalar). The result is byte-offset mismatches that cause rendering corruption on
Vulkan and Metal.

## Scope

**Every struct** that is used in a `ConstantBuffer<T>` or `StructuredBuffer<T>` in Slang
shaders, and its corresponding C# mirror struct, must be refactored so that all members
are `float4`-aligned. This includes:

- All `.slang` shader files under `sources/Experiments/SponzaScene/Assets/Shaders/`
- All C# `file struct` definitions in `sources/Experiments/SponzaScene/Renderer/Passes/`
- All C# shared model structs in `sources/Experiments/SponzaScene/Models/`
  (e.g., `DirectionalLight`, `PointLight`, `CSMData`)
- All inline shader source strings and C# struct definitions in tutorial `.md` files
  under `documents/tutorials/`

## What NOT to change

- **Vertex buffer structs** and their `InputLayout` definitions — vertex input layout is
  explicitly declared and consistent across backends. Do NOT modify `Vertex` structs used
  with vertex buffers (e.g., the `Vertex` struct in `Models/Vertex.cs` or tutorial vertex
  structs with `POSITION`/`NORMAL`/`TEXCOORD`/`COLOR` semantics).
- **Shader I/O structs** (`VSInput`, `PSInput`, `PSOutput`, `VertexOutput`, etc.) — these
  are controlled by semantic bindings, not memory layout.

## Rules

1. Every struct member used in `ConstantBuffer<T>` or `StructuredBuffer<T>` must be one of:
   `float4` (`Vector4`), `float4x4` (`Matrix4x4`), `int4`, `uint4`, or a group of scalars
   that exactly fills 16 bytes (e.g., four `float`/`int`/`uint` fields).
   No `float2`, `float3`, or lone trailing scalars.
2. Pack related data into `float4` (e.g., `float3 Position` + `float Radius` →
   `float4 PositionAndRadius`), and update all shader read sites to use `.xyz` / `.w`.
3. When a struct embeds another struct that contains `float3` members
   (e.g., `DirectionalLight` inside `LightingConstants`), inline those fields as `float4`
   members directly.
4. The C# mirror struct must be byte-for-byte identical to the Slang struct.
   Its `sizeof()` must be a multiple of 16.
5. Update all shader code that reads from modified fields to use the new `.xyzw` accessors.
6. Update all C# code that writes to modified structs to populate the new packed fields.

## Documentation Changes

If any existing tutorial `.md` files under `documents/tutorials/` contain explanations
about struct layout or data alignment for GPU buffers, **remove those sections**.
