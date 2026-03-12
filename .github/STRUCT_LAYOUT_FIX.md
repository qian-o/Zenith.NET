# Fix all GPU struct layouts for cross-backend compatibility (DX12 / Vulkan / Metal)

## Background

Slang compiles shaders to DXIL, SPIR-V, and metallib. Each backend applies different
alignment and packing rules to struct members containing `float2`, `float3`, or mixed
scalars. This affects both `ConstantBuffer<T>` (std140 / cbuffer / MSL rules) and
`StructuredBuffer<T>` (std430 vs scalar layout — `vec3` aligns to 16 in std430 but 4
in scalar). The result is byte-offset mismatches that cause rendering corruption on
Vulkan and Metal.

## Approach

Instead of renaming fields to `float4`, **insert explicit `private` padding fields in Slang**
to ensure every 16-byte row is fully occupied. On the **C# side**, use
`[StructLayout(LayoutKind.Explicit, Size = N)]` with `[FieldOffset]` attributes instead of
padding fields — this avoids unused-variable warnings and keeps the C# structs clean.

This preserves original field names and types, minimizes changes to shader logic code.

## Scope

**Every struct** that is used in a `ConstantBuffer<T>` or `StructuredBuffer<T>` in Slang
shaders, and its corresponding C# mirror struct, must be padded so that all members are
aligned to 16-byte boundaries. This includes:

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

1. **16-byte row alignment**: Every `float3` or `float2` must be followed by enough padding
   to complete a 16-byte row. Lone trailing scalars (`float`, `int`, `uint`) must also be
   padded to fill a complete 16-byte row. Allowed row compositions:
   - `float4x4` (64 bytes, naturally aligned)
   - `float4` / `int4` / `uint4` (16 bytes)
   - `float3` + `float` (12 + 4 = 16 bytes)
   - `float2` + `float2` (8 + 8 = 16 bytes)
   - `float2` + 2 scalars (8 + 4 + 4 = 16 bytes)
   - 4 scalars (4 × 4 = 16 bytes)

2. **Slang padding field style**: Use `private` padding fields named `padding0`, `padding1`,
   etc. (no underscore prefix). Use `private float paddingN;` or `private float2 paddingN;`.
   Insert a blank line between every field declaration (including padding fields).
   Example:
   ```
   struct BloomConstants
   {
       float2 TexelSize;

       private float2 padding0;
   };
   ```

3. **C# struct style**: Use `[StructLayout(LayoutKind.Explicit, Size = N)]` where `N` is
   the total struct size (must be a multiple of 16). Use `[FieldOffset(X)]` on each field.
   Do NOT add padding fields in C# — the `Size` parameter and `FieldOffset` gaps handle
   alignment. Insert a blank line between every field declaration. Example:
   ```csharp
   [StructLayout(LayoutKind.Explicit, Size = 16)]
   file struct BloomConstants
   {
       [FieldOffset(0)]
       public Vector2 TexelSize;
   }
   ```

4. **Do NOT rename or retype existing fields**. Keep `float3 Direction` as `float3 Direction`,
   keep `float2 TexelSize` as `float2 TexelSize`. Only add padding fields in Slang.

5. **Struct tail alignment**: The `Size` parameter in `[StructLayout]` on the C# side and
   tail padding in Slang must ensure the total size is a **multiple of 16 bytes**. This
   guarantees correct stride when the struct is used in arrays (e.g., `StructuredBuffer<T>`
   or `T[]` uploads).

6. **Embedded structs**: When a struct embeds another struct (e.g., `DirectionalLight`
   inside `LightingConstants`), the embedded struct itself must already be padded to a
   16-byte multiple. Do NOT inline/flatten the embedded struct — just ensure it is
   independently padded.

7. **C# struct must be byte-for-byte identical in layout** to the Slang struct. The
   `[FieldOffset]` values must match the byte offsets produced by the Slang struct
   (including its padding fields). The `Size` must match the Slang struct's total size.

8. **Do NOT modify shader logic code**. No field reads or writes should need updating.
   The only Slang changes are adding `private` padding fields. The only C# changes are
   adding `[StructLayout]` and `[FieldOffset]` attributes.

## Documentation Changes

If any existing tutorial `.md` files under `documents/tutorials/` contain explanations
about struct layout or data alignment for GPU buffers, **remove those sections**.
