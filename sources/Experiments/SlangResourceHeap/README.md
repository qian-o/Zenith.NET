# Slang ResourceHeap Minimal Tests

This folder is independent from the Zenith.NET runtime projects. It contains a
small C# console app that references `Slangc.NET` directly, plus three Slang
shader files. The goal is to test whether the proposed public model can be
written as:

```text
ConstantBuffer<Params> + ResourceHeap + SamplerHeap
```

The shaders rely on Slang `DescriptorHandle<T>`. Compilation is driven through
the NuGet `Slangc.NET` package instead of any `slangc.exe` that may appear on
`PATH`.

## Binding Contract

- One `ConstantBuffer<T>` containing ordinary parameters and typed handles.
  Vulkan pins it to set 0 binding 0; DXIL lets Slang auto-assign the first CBO.
- Resource heap: descriptors referenced by `DescriptorHandle<Texture2D<T>>`,
  `DescriptorHandle<StructuredBuffer<T>>`, and
  `DescriptorHandle<RWStructuredBuffer<T>>`.
- Sampler heap: descriptors referenced by `DescriptorHandle<SamplerState>`.
- Vulkan uses `-bindless-space-index 100` so Slang's global bindless arrays do
  not collide with app set 0 in these tests.
- DXIL uses `sm_6_6`, which is required for direct heap indexing.

## Test Cases

1. `Shaders/01_cbo_texture_sampler.slang`
   - Fragment shader samples a texture handle and sampler handle stored in CBO.
2. `Shaders/02_cbo_buffer_uav.slang`
   - Compute shader reads a structured buffer handle and writes an RW structured
     buffer handle stored in CBO.
3. `Shaders/03_nonuniform_material_texture.slang`
   - Fragment shader reads a material record from a structured buffer handle.
     Each material contains a texture handle, and the shader uses `nonuniform`
     before dereferencing it.
4. `Shaders/04_texture_handle_array.slang`
   - Fragment shader reads a texture handle from a structured buffer of texture
     handles, then samples it with `nonuniform(handle)`.

## Many Textures

For many textures, shader code should not use an untyped `heap[index]` expression
directly. The portable Slang shape is to store typed handles in ordinary data,
such as a constant buffer, material buffer, or a structured buffer of handles:

```hlsl
struct DrawConstants
{
    DescriptorHandle<StructuredBuffer<DescriptorHandle<Texture2D<float4>>>> Textures;
    DescriptorHandle<SamplerState> LinearSampler;
    uint TextureIndex;
};

DescriptorHandle<Texture2D<float4>> textureHandle = (*draw.Textures)[draw.TextureIndex];
float4 color = nonuniform(textureHandle)->Sample(*draw.LinearSampler, uv);
```

The host-side `ResourceHeap` owns the actual descriptors. The shader receives
typed descriptor handles or buffers of typed descriptor handles, and Slang lowers
the handle dereference to the target's heap or bindless descriptor mechanism.

## Compile

Start the C# project directly to run the default platform test matrix. No
arguments is equivalent to `all --clean --spv-descriptor-heap-ext`:

```powershell
dotnet run --project SlangResourceHeap.csproj
```

Use explicit arguments when you want a narrower target:

```powershell
dotnet run --project SlangResourceHeap.csproj -- all --clean
dotnet run --project SlangResourceHeap.csproj -- dxil
dotnet run --project SlangResourceHeap.csproj -- spirv
dotnet run --project SlangResourceHeap.csproj -- spirv --spv-descriptor-heap-ext
dotnet run --project SlangResourceHeap.csproj -- metal-source
dotnet run --project SlangResourceHeap.csproj -- metal
```

The `all` target is host-aware:

- Windows: DXIL, SPIR-V, and Metal source.
- macOS: SPIR-V, Metal source, and `metallib`.
- Linux: SPIR-V and Metal source.

`metal-source` emits MSL and does not require Apple's Metal compiler. `metal`
emits `metallib` with `-target metallib -capability metallib_latest` and passes
`-std=metal4.0` to Apple's downstream `metal` compiler, so it is the target to
use when validating the Apple Metal 4 library path. On macOS and Linux, `all`
skips DXIL because Slang's DXIL target needs a loadable DXC / `dxcompiler`
downstream compiler. Run `dxil` explicitly only on a machine where DXC is
installed and loadable by Slang.

The `metal` target emits `metallib` and is intended for a machine with the Metal
downstream compiler available. The project writes outputs under `out/`. It does
not run any host-side binding code.

If `metal-source` succeeds but `metal` fails on a non-Apple machine, the missing
piece is the platform Metal compiler rather than the CBO/ResourceHeap shader
syntax.

This experiment uses the repository's central `Slangc.NET` package version, so
updating `sources/Directory.Packages.props` is enough to move the compiler used
by the test app.