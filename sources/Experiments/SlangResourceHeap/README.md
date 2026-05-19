# Zenith DescriptorHandle Minimal Tests

This folder is independent from the Zenith.NET runtime projects. It contains a
small C# console app that references `Slangc.NET` directly, plus Slang shader
files. The current experiment follows the Slang-official `DescriptorHandle<T>`
model instead of a Zenith-owned shader compatibility layer.

The proposed public shape is:

```text
ConstantBuffer<Params> + DescriptorHandle<T> + backend-specific binding API
```

Compilation is driven through the NuGet `Slangc.NET` package instead of any
`slangc.exe` that may appear on `PATH`.

## Route A Contract

- Shader code uses Slang's native `DescriptorHandle<T>` type directly.
- DXIL and SPIR-V may treat descriptor handles as integer descriptor slots. In
  practice, Slang's default descriptor-handle dereference logic lowers to
  direct heap indexing or bindless descriptor arrays.
- Metal must not be forced into the same integer ABI. Slang intentionally lowers
  `DescriptorHandle<T>` to the native layout of `T` on Metal:

  ```text
  DescriptorHandle<Texture2D<float4>>              -> texture2d<float, access::sample>
  DescriptorHandle<SamplerState>                  -> sampler
  DescriptorHandle<StructuredBuffer<T>>           -> device T*
  StructuredBuffer<DescriptorHandle<Texture2D<T>>> -> texture2d<...> device*
  ```

- Therefore the Metal backend should handle compatibility in the runtime API,
  not in shader source. Metal should follow reflection and bind native resources
  or fill Metal argument buffers, similar to Slang-RHI.

The important design split is:

```text
DX12/Vulkan:
    resource.GetDescriptorHandle() -> integer slot -> setDescriptorHandle()

Metal:
    resource binding / ParameterBlock / argument buffer -> native resource id or gpu address
```

## Official Slang-RHI Behavior

Slang-RHI exposes a host-side descriptor handle type:

```cpp
struct DescriptorHandle
{
    DescriptorHandleType type;
    uint64_t value;
};
```

On D3D12 and Vulkan, `getDescriptorHandle` allocates a bindless descriptor slot
and `setDescriptorHandle` writes the 8-byte `value` into shader object uniform
data.

On Metal, resource `getDescriptorHandle` is not implemented. The official API
support table marks Metal as unsupported for:

- `IBuffer::getDescriptorHandle`
- `ITextureView::getDescriptorHandle`
- `ISampler::getDescriptorHandle`
- `IAccelerationStructure::getDescriptorHandle`

The Metal backend instead binds native resources. Its argument-buffer path writes
Metal `gpuResourceID()` values for textures, samplers, and acceleration
structures, and writes `gpuAddress()` values for buffers. If residency sets are
not available, it tracks resources for per-encoder `useResource` fallback.

Zenith should mirror that policy for Route A: keep shader code official, and let
the Metal API layer translate application resource bindings to Metal-native
layout.

## Shader Usage

Use `DescriptorHandle<T>` in parameter structs, then let Slang implicitly
dereference handles when a resource value is needed. The official docs state
that `getDescriptorFromHandle` is not supposed to be called from user code
directly; it is a customization hook that an engine can provide to override the
default dereference behavior.

```hlsl
struct DrawParams
{
    DescriptorHandle<Texture2D<float4>> BaseColorTexture;
    DescriptorHandle<SamplerState> LinearSampler;
    float4 Tint;
};

ConstantBuffer<DrawParams> gDraw : register(b0);

float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
{
    return gDraw.BaseColorTexture->Sample(gDraw.LinearSampler, uv) * gDraw.Tint;
}
```

When a descriptor handle value is loaded through a per-thread-varying path, mark
it non-uniform immediately before dereferencing:

```hlsl
DescriptorHandle<Texture2D<float4>> handle = materials[index].BaseColorTexture;
float4 color = nonuniform(handle)->Sample(sampler, uv);
```

For buffer resources, the handle can be used as if it were the underlying
buffer:

```hlsl
gBuffers.Destination[index] = gBuffers.Source[index] * scale;
```

## Resource Binding API Direction

The C# side should expose a single backend-neutral `ResourceHandle` value as the
host counterpart of shader-side `DescriptorHandle<T>`. It is not a custom Slang
shader type; shaders should keep using the official `DescriptorHandle<T>`.

```csharp
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct ResourceHandle
{
  public uint X;
  public uint Y;
}

public abstract class Buffer
{
    public ResourceHandle UniformHandle { get; }
    public ResourceHandle StorageReadOnlyHandle { get; }
    public ResourceHandle StorageReadWriteHandle { get; }
}

  public abstract class BufferView
  {
    public ResourceHandle UniformHandle { get; }
    public ResourceHandle StorageReadOnlyHandle { get; }
    public ResourceHandle StorageReadWriteHandle { get; }
  }

public abstract class Texture
{
    public ResourceHandle SampledHandle { get; }
    public ResourceHandle StorageHandle { get; }
}

  public abstract class TextureView
  {
    public ResourceHandle SampledHandle { get; }
    public ResourceHandle StorageHandle { get; }
  }

public abstract class Sampler
{
    public ResourceHandle Handle { get; }
}
```

Draw constants can then stay resource-oriented and backend-neutral:

```csharp
public struct DrawParams
{
    public Matrix4x4 World;
    public Matrix4x4 ViewProjection;
    public ResourceHandle MaterialBuffer;
    public ResourceHandle BaseColor;
    public ResourceHandle LinearSampler;
    public uint MaterialIndex;
}

cmd.SetPipeline(pipeline);
cmd.SetConstants(new DrawParams
{
    World = world,
    ViewProjection = camera.ViewProjection,
    MaterialBuffer = materialBuffer.StorageReadOnlyHandle,
    BaseColor = texture.SampledHandle,
    LinearSampler = sampler.Handle,
    MaterialIndex = materialIndex,
});
cmd.DrawIndexed(indexCount);
```

Backend policy:

- DX12/Vulkan: `ResourceHandle` stores descriptor-heap indexes, so
  `SetConstants<T>` can copy the struct bytes directly into uniform data.
- Metal: `SetConstants<T>` must be reflection-aware. It copies ordinary POD
  fields, resolves `ResourceHandle` fields to native Metal resources, and writes
  those resources into a command/frame argument table. The user-facing C# struct
  stays the same, but Metal does not upload the `ResourceHandle` bytes as the
  final shader ABI.
- For CPU-authored buffers that contain `ResourceHandle` values, provide a
  backend-aware upload/packing path instead of raw byte upload on Metal. Large
  material systems should usually store resource indices and bind resource arrays
  or argument tables explicitly.

This keeps the public API simple while preserving the official Slang rule:
shader source uses `DescriptorHandle<T>`, and backend-specific ABI work happens
in the runtime binding layer.

## Metal API Implications

The experiment still compiles Metal source for `DescriptorHandle<T>` so we can
inspect Slang's generated MSL. That does not mean Zenith should upload the same
CPU byte layout to Metal.

For Metal, Zenith should build a pipeline binding manifest from Slang
reflection:

- Ordinary POD fields are copied into Metal constant data.
- Texture fields are bound as `MTLTexture` or written as argument-buffer resource
  IDs.
- Sampler fields are bound as `MTLSamplerState` or written as argument-buffer
  resource IDs.
- Buffer fields are bound as `MTLBuffer`/offset or written as GPU addresses.
- Pointer/resource fields referenced from ordinary data must be included in
  residency tracking.

That means a high-level call can stay simple:

```csharp
cmd.SetPipeline(pipeline);
cmd.SetConstants(drawParams);
cmd.DrawIndexed(...);
```

but `SetConstants` is backend-aware:

- DX12/Vulkan encode descriptor slot integers into the uniform data.
- Metal resolves resource references and populates native bindings or argument
  buffers according to reflection.

## Test Cases

1. `Shaders/01_cbo_texture_sampler.slang`
   - Fragment shader samples a `DescriptorHandle<Texture2D<float4>>` and
     `DescriptorHandle<SamplerState>` stored in a CBO.
2. `Shaders/02_cbo_buffer_uav.slang`
   - Compute shader reads a `DescriptorHandle<StructuredBuffer<float4>>` and
     writes a `DescriptorHandle<RWStructuredBuffer<float4>>` stored in a CBO.
3. `Shaders/03_nonuniform_material_texture.slang`
   - Fragment shader reads a material record from a structured-buffer handle.
     Each material contains a `DescriptorHandle<Texture2D<float4>>`.
4. `Shaders/04_texture_handle_array.slang`
   - Fragment shader reads a descriptor handle directly from a structured buffer
     of texture handles:
     `DescriptorHandle<StructuredBuffer<DescriptorHandle<Texture2D<float4>>>>`.

The fourth case is intentionally a stress case. It is natural on DX12/Vulkan,
but Metal lowers the nested texture handles to native texture-resource layout.
Zenith should treat this as a feature to inspect, not necessarily the preferred
Metal data model. For large material systems, a more Metal-friendly production
model is often `uint` material texture indices plus API-bound texture arrays or
argument buffers.

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
piece is the platform Metal compiler rather than the shader syntax.

This experiment uses the repository's central `Slangc.NET` package version, so
updating `sources/Directory.Packages.props` is enough to move the compiler used
by the test app.