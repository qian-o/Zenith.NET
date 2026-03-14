# Shader Struct Alignment Fix Guide

## Problem

`float3` / `int3` / `uint3` types have inconsistent sizes across graphics APIs. On DX12/Vulkan they are 12 bytes, but on Metal they are **16 bytes**. This means `float3 + float` packs into 16 bytes on DX12/Vulkan, but becomes **32 bytes** on Metal. The `float3; float padding;` pattern does NOT work on Metal because `float3` is already 16 bytes.

## Core Fix

**In shader code, merge each `xxx3` field with the immediately following scalar field into a `private xxx4`, then expose the original fields via `property` accessors.** This guarantees 16-byte alignment on all platforms without changing any shader access code.

For example: `float3 Direction; float Intensity;` -> `private float4 DirectionAndIntensity;` + `property float3 Direction` / `property float Intensity`

If the next field is a `private float padding`, merge it the same way. If there is no next scalar field, the `.w` component is unused.

**Type mismatch:** Merging is only possible when `xxx3` and the next scalar share the same base type (e.g., `float3` + `float`). If they differ (e.g., `uint3` + `float`), expand the `xxx3` to `xxx4` with `.w` unused, and keep the next scalar as a separate field.

## Rules

### Rule 1: Never use `xxx3` types in buffer structs

**Forbidden:** `float3` / `int3` / `uint3` in any `ConstantBuffer<T>` or `StructuredBuffer<T>` struct definition.

Safe types: `float`, `int`, `uint`, `float2`, `int2`, `uint2`, `float4`, `int4`, `uint4`, `float4x4`.

> **Note:** `float3` is still perfectly fine as **local variables**, **function parameters**, **function return types**, and **interpolated vertex outputs** — only avoid it inside buffer-backed struct definitions.

### Rule 2: Struct total size must be a multiple of 16 bytes

If the struct does not naturally end on a 16-byte boundary, add `float padding0`, `float padding1`, ... fields **in the shader** to reach the next 16-byte multiple.

### Rule 3: C# side keeps original field names and types

- Use `[StructLayout(LayoutKind.Explicit, Size = N)]` where `N` is a multiple of 16.
- Use `[FieldOffset(X)]` for each field.
- **Keep original semantic field names** (e.g., `Direction`, `Intensity`, `Color`). Do NOT mirror shader packing names like `DirectionAndIntensity` or `ColorAndPadding`.
- **No padding fields needed on C# side** — the `Size` parameter handles tail alignment.
- **No need to change field types** — `Vector3`, `float`, etc. stay as-is. `FieldOffset` + `Size` guarantee correct layout.

## Example

### Before (broken on Metal)

Shader (`.slang`):
```hlsl
struct DirectionalLight
{
    float3 Direction;

    float Intensity;

    float3 Color;

    private float padding0;
};
```

C# side:
```csharp
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct DirectionalLight
{
    [FieldOffset(0)]
    public Vector3 Direction;

    [FieldOffset(12)]
    public float Intensity;

    [FieldOffset(16)]
    public Vector3 Color;
}
```

### After (works on DX12 / Vulkan / Metal)

Shader (`.slang`) — merge `float3` + next scalar -> `float4`:
```hlsl
struct DirectionalLight
{
    private float4 DirectionAndIntensity;

    private float4 ColorAndPadding;

    property float3 Direction { get { return DirectionAndIntensity.xyz; } }

    property float Intensity { get { return DirectionAndIntensity.w; } }

    property float3 Color { get { return ColorAndPadding.xyz; } }
};
```

C# side — **unchanged**, `FieldOffset` already ensures correct layout:
```csharp
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct DirectionalLight
{
    [FieldOffset(0)]
    public Vector3 Direction;

    [FieldOffset(12)]
    public float Intensity;

    [FieldOffset(16)]
    public Vector3 Color;
}
```

## Scope

### NEED fixing

Any `.slang` struct used with `ConstantBuffer<T>` or `StructuredBuffer<T>`, and its corresponding C# struct. This includes all experiment shaders and tutorial docs under `sources/Experiments/` and `documents/tutorials/`.

### DO NOT fix

- Vertex input structs (`VSInput` with semantics like `POSITION0`, `NORMAL0`) — handled by input assembler
- Stage I/O structs (`PSInput`, `VertexOutput` with interpolated semantics) — not buffer-backed
- Shader-local structs not bound to any buffer (e.g., `SkyParams`)
- C# vertex structs used with vertex buffer + `InputLayout`, not `StructuredBuffer`
- `float3` / `uint3` used as local variables, function parameters, return types, or system value semantics (e.g., `uint3 dispatchThreadID : SV_DispatchThreadID`)

## Steps

1. **Shader structs:** In every buffer-backed struct, merge each `xxx3` field with the immediately following scalar into a `private xxx4` field (only when they share the same base type), then add `property` accessors to expose the original field names. If types differ, expand `xxx3` to `xxx4` (`.w` unused) and keep the next scalar separate. Add `float padding0`, `float padding1`, ... if the total size is not a multiple of 16.

2. **C# structs:** Only update `FieldOffset` values and `Size` if field positions changed due to shader repacking. Keep original field names and types.
