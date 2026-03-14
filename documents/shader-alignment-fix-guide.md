# Shader Struct Alignment Fix Guide

## Problem

`float3` / `int3` / `uint3` types have inconsistent sizes across graphics APIs:

| Type | DX12 / Vulkan | Metal |
|------|--------------|-------|
| `float3` / `int3` / `uint3` | 12 bytes | **16 bytes** |

This means `float3 + float` packs into 16 bytes on DX12/Vulkan, but becomes **32 bytes** on Metal. The `float3; float padding;` pattern does NOT work on Metal because `float3` is already 16 bytes.

## Rules

### Rule 1: Never use `xxx3` types in buffer structs

Only use these types in constant buffer / structured buffer structs:

| Safe Types | Size | Cross-platform |
|------------|------|----------------|
| `float` / `int` / `uint` | 4B | ✅ |
| `float2` / `int2` / `uint2` | 8B | ✅ |
| `float4` / `int4` / `uint4` | 16B | ✅ |
| `float4x4` | 64B | ✅ |

**Forbidden:** `float3` / `int3` / `uint3` in any buffer struct definition.

> **Note:** `float3` is still perfectly fine to use as **local variables**, **function parameters**, **function return types**, and **interpolated vertex outputs** — only avoid it inside buffer-backed struct definitions.

### Rule 2: Pack `xxx3` data into `xxx4`

When a struct field semantically holds 3 components, use `float4` / `int4` / `uint4` and pack related scalar into the `.w` component. If no related scalar exists, `.w` is unused.

### Rule 3: Struct total size must be a multiple of 16 bytes

If the struct does not naturally end on a 16-byte boundary, add `float padding0`, `float padding1`, ... fields **in the shader** to reach the next 16-byte multiple.

### Rule 4: C# side uses `StructLayout(Explicit)` + `Size`

- Use `[StructLayout(LayoutKind.Explicit, Size = N)]` where `N` is a multiple of 16.
- Use `[FieldOffset(X)]` for each field.
- **No padding fields needed on C# side** — the `Size` parameter handles tail alignment.

## Transformation Examples

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

Shader (`.slang`):
```hlsl
struct DirectionalLight
{
    float4 DirectionAndIntensity; // xyz = Direction, w = Intensity
    float4 ColorAndPadding;       // xyz = Color, w = unused
};
```

C# side:
```csharp
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct DirectionalLight
{
    [FieldOffset(0)]
    public Vector4 DirectionAndIntensity; // XYZ = Direction, W = Intensity

    [FieldOffset(16)]
    public Vector4 ColorAndPadding;       // XYZ = Color, W = unused
}
```

## What to fix

1. **All `.slang` shader files:** Find every struct used with `ConstantBuffer<T>` or `StructuredBuffer<T>`. Replace any `float3` / `int3` / `uint3` fields with `float4` / `int4` / `uint4`. Add `float padding0`, `float padding1`, ... if the total size is not a multiple of 16.

2. **All C# struct definitions** that map to shader structs: Replace `Vector3` fields with `Vector4`, update `FieldOffset` values and `Size` accordingly. No padding fields needed on C# side.

3. **All shader code that reads these fields:** Update access patterns (e.g., `light.Direction` → `light.DirectionAndIntensity.xyz`).

4. **All C# code that writes these fields:** Update to construct `Vector4` values (e.g., `new Vector4(dir, intensity)` instead of separate `Direction` and `Intensity` assignments).

5. **Tutorial markdown files** (`documents/tutorials/`): Update any struct definitions and code examples that use `Vector3` / `float3` in buffer structs.
