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
- CPU-authored buffers must not contain shader-side `DescriptorHandle<T>` values
  when Metal is a target. Large material systems should store resource indices
  and bind resource arrays or argument tables explicitly.

This keeps the public API simple while preserving the official Slang rule:
shader source uses `DescriptorHandle<T>`, and backend-specific ABI work happens
in the runtime binding layer.

## AOT Binding Generation

Metal needs resource-layout information, but that does not require runtime
.NET reflection. The preferred AOT path is to move reflection to build time:

1. The user marks constants structs with `[Constants]` and declares them as
   `partial`.
2. A shader build step compiles Slang and extracts reflection for each entry
   point, including parameter blocks, `DescriptorHandle<T>` fields, ordinary POD
   fields, Metal argument-buffer indexes, resource kinds, access modes, and byte
   offsets.
3. A C# source generator matches that shader reflection with user constants
   structs such as `DrawParams` and validates that every `ResourceHandle` field
   corresponds to the expected shader-side `DescriptorHandle<T>` field.
4. The generator emits the other partial declaration that implements a static
   layout interface. The runtime calls the static interface member instead of
   inspecting fields through reflection.
5. The runtime command path constrains `SetConstants<T>` to the layout
   interface:

   ```csharp
   cmd.SetConstants(in drawParams);
   ```

The user-authored side stays small:

```csharp
[Constants]
public partial struct DrawParams
{
    public Matrix4x4 World;
    public Matrix4x4 ViewProjection;
    public ResourceHandle Buffers;
    public ResourceHandle BaseColor;
    public ResourceHandle LinearSampler;
    public uint MaterialIndex;
}
```

The generated side supplies the AOT layout contract:

```csharp
public interface IConstantsLayout<TSelf>
    where TSelf : unmanaged, IConstantsLayout<TSelf>
{
    static abstract ConstantsLayout GetLayout();
}

public partial struct DrawParams : IConstantsLayout<DrawParams>
{
    public static ConstantsLayout GetLayout() => DrawParamsLayout.Value;
}
```

`ConstantsLayout` should be a manifest that contains per-backend layouts derived
from `SlangReflection.Json`, not a single universal byte layout. Fixed resource
arrays prove that DXIL, SPIR-V, and Metal can all differ for the same source.

Then the public command API can be both simple and AOT-safe:

```csharp
public void SetConstants<TConstants>(in TConstants constants)
    where TConstants : unmanaged, IConstantsLayout<TConstants>
{
    ConstantsLayout layout = TConstants.GetLayout();
  BackendConstantsLayout backendLayout = layout.For(Context.Backend);
  // DX/VK pack bytes from generated offsets; Metal consumes resource slots.
}
```

Generated layout shape:

```csharp
file static class DrawParamsLayout
{
    public static readonly ConstantsLayout Value = new(
      sizeInBytes: 160,
        resourceFields:
        [
            new("Buffers", ResourceKind.Buffer, ResourceAccess.Read, offsetInBytes: 128, metalIndex: 0),
            new("BaseColor", ResourceKind.Texture, ResourceAccess.Read, offsetInBytes: 136, metalIndex: 0),
            new("LinearSampler", ResourceKind.Sampler, ResourceAccess.Read, offsetInBytes: 144, metalIndex: 0),
        ]);
}
```

The runtime still has one generic implementation, but it consumes generated
metadata rather than reflection metadata:

- DX12/Vulkan copy the bytes directly because the `ResourceHandle` values are
  already descriptor indexes.
- Metal copies POD bytes and patches resource fields into the generated Metal
  argument layout by resolving `ResourceHandle` to `MTLBuffer` GPU address,
  `MTLTexture`, or `MTLSamplerState`.

For maximum AOT friendliness, keep the generated path explicit. Do not discover
binders by scanning assemblies. Either call the generated binding type directly,
or have the shader/pipeline source generator emit pipeline factory code that
registers the manifest through ordinary static references.

Important limitation: the layout interface cannot make this shader type portable
to Metal:

```slang
DescriptorHandle<StructuredBuffer<DescriptorHandle<Texture2D<float4>>>> Textures;
```

DX12 and Vulkan can treat the buffer payload as a sequence of descriptor-handle
tokens, but Slang lowers the Metal version to a buffer of native texture
resources:

```metal
texture2d<float, access::sample> device* Textures;
```

That is not an ordinary `MTLBuffer` payload that Zenith can assemble from a C#
`ResourceHandle[]`. A generator can know the recursive type, but it still cannot
write texture objects into a structured-buffer byte stream for Metal.

The AOT generator should therefore diagnose this pattern when Metal is enabled,
for example:

```text
ZENITH0001: StructuredBuffer<DescriptorHandle<T>> is not portable to Metal.
Use uint resource indices plus an explicit resource table/argument table.
```

The portable model is:

```slang
struct TextureArrayConstants
{
  DescriptorHandle<StructuredBuffer<uint>> TextureIndices;
  DescriptorHandle<SamplerState> LinearSampler;
  uint TextureCount;
  float UvScale;
};

// Bound by the generated pipeline binding manifest.
Texture2D<float4> gTextureTable[];

ConstantBuffer<TextureArrayConstants> gTextures;

float4 sampleTexture(uint logicalIndex, float2 uv)
{
  uint textureIndex = gTextures.TextureIndices[logicalIndex];
  return gTextureTable[nonuniform(textureIndex)].Sample(gTextures.LinearSampler, uv);
}
```

The generated constants layout then only describes the index buffer and sampler:

```csharp
file static class TextureIndexParamsLayout
{
    public static readonly ConstantsLayout Value = new(
        sizeInBytes: 32,
        resourceFields:
        [
      new("TextureIndices", ResourceKind.Buffer, ResourceAccess.Read, offsetInBytes: 0, metalIndex: 0),
            new("LinearSampler", ResourceKind.Sampler, ResourceAccess.Read, offsetInBytes: 8, metalIndex: 0),
        ]);
}
```

The texture table itself is a pipeline/resource-table binding generated from
shader reflection. DX12/Vulkan may still implement it as descriptor heap indexes;
Metal implements it as an argument table/resource array. Users who do not want
the generator can manually implement `IConstantsLayout<T>`, but they still need
to avoid `StructuredBuffer<DescriptorHandle<T>>` for portable Metal shaders.

Fixed-size arrays are different. This shader shape is portable as a small,
compile-time-sized binding group:

```slang
static const uint TextureSlotCount = 4;

struct FixedTextureArrayConstants
{
  DescriptorHandle<Texture2D<float4>> Textures[TextureSlotCount];
  DescriptorHandle<SamplerState> LinearSampler;
  uint TextureIndex;
  float UvScale;
};
```

Slang lowers it to `uint2 Textures[4]` for HLSL/DXIL-style targets and to a
Metal native resource array:

```metal
array<texture2d<float, access::sample>, int(4)> Textures;
```

`SlangCompiler.CompileWithReflection(args, out SlangReflection reflection)`
returns the compiled bytes and exposes `reflection.Json`. For this fixed array,
the JSON shows that every backend needs its own generated layout:

```text
DXIL:
  Textures      uniform offset 0,  size 56, elementStride 16
  LinearSampler uniform offset 56, size 8
  TextureIndex  uniform offset 64, size 4
  UvScale       uniform offset 68, size 4
  CBO size      72

SPIR-V + spvDescriptorHeapEXT:
  Textures      uniform offset 0,  size 64, elementStride 16
  LinearSampler uniform offset 64, size 8
  TextureIndex  uniform offset 72, size 4
  UvScale       uniform offset 76, size 4
  CBO size      80

Metal:
  Textures      shaderResource index 0, count 4
  LinearSampler samplerState index 0
  TextureIndex  uniform offset 0, size 4
  UvScale       uniform offset 4, size 4
  CBO size      8
```

That means fixed arrays can be supported, but they are not ordinary tight
`ResourceHandle[]` data on every backend. Even DXIL and SPIR-V differ in the
uniform size for the same Slang source. The source generator must consume Slang
reflection per target and emit a per-backend layout, not just preserve C# struct
field order.

The generated layout should therefore distinguish source offsets, shader uniform
offsets, array strides, and Metal resource slots. A simplified SPIR-V layout
looks like this:

```csharp
file static class FixedTextureArrayParamsLayout
{
    public static readonly ConstantsLayout Spirv = new(
        sizeInBytes: 80,
        resourceFields:
        [
            new("Textures", ResourceKind.Texture, ResourceAccess.Read, offsetInBytes: 0, metalIndex: 0, count: 4, strideInBytes: 16),
            new("LinearSampler", ResourceKind.Sampler, ResourceAccess.Read, offsetInBytes: 64, metalIndex: 0),
        ]);
}
```

The fixed-length C# value type can also be generated. A source generator cannot
rewrite a field type, so this is not possible:

```csharp
[ResourceHandleArray(4)]
public ResourceHandle Textures; // Cannot be rewritten into an inline array.
```

Instead, let the user declare a named partial value type and let the generator
complete it:

```csharp
[ResourceHandleArray(4)]
public partial struct TextureHandle4
{
}
```

Generated source:

```csharp
[InlineArray(4)]
public partial struct TextureHandle4
{
  private ResourceHandle element0;
}
```

Then constants structs use the generated value type:

```csharp
[Constants]
public partial struct FixedTextureArrayParams
{
  public TextureHandle4 Textures;
  public ResourceHandle LinearSampler;
  public uint TextureIndex;
  public float UvScale;
}
```

`TextureHandle4` is unmanaged and indexable, but the user does not write the
`InlineArray` backing field. The generator can ship common predefined types such
as `ResourceHandle2`, `ResourceHandle4`, `ResourceHandle8`, and
`ResourceHandle16`, and also support project-local marker declarations for less
common lengths. Shader reflection still decides whether a field named `Textures`
matches `DescriptorHandle<Texture2D<float4>>[4]`, so the array type itself does
not need to encode texture/buffer/sampler kind.

Metal operation model:

- The Metal constants buffer only contains ordinary POD fields here:
  `TextureIndex` and `UvScale`.
- `Textures[0..3]` are flattened into four reflected texture resource slots.
- `SetConstants` reads the four C# `ResourceHandle` values, resolves them to
  `MTLTexture` objects or resource IDs, and writes them into the command/frame
  argument table at `metalIndex + elementIndex`.
- The sampler is resolved separately into the sampler table.

This is a good model for small fixed groups such as four material textures. It
is not a good model for large or frequently changing arrays, because Metal work
is proportional to the fixed count. For large sets, use `uint` indices plus a
resource table/argument table object that can be cached and rebound.

Flattening the fixed group into named fields avoids the constant-buffer array
stride difference between DXIL and SPIR-V:

```slang
struct FlattenedTextureConstants
{
  DescriptorHandle<Texture2D<float4>> Texture0;
  DescriptorHandle<Texture2D<float4>> Texture1;
  DescriptorHandle<Texture2D<float4>> Texture2;
  DescriptorHandle<Texture2D<float4>> Texture3;
  DescriptorHandle<SamplerState> LinearSampler;
  uint TextureIndex;
  float UvScale;
};
```

Reflection for both DXIL and SPIR-V reports the same uniform layout:

```text
Texture0      offset 0,  size 8
Texture1      offset 8,  size 8
Texture2      offset 16, size 8
Texture3      offset 24, size 8
LinearSampler offset 32, size 8
TextureIndex  offset 40, size 4
UvScale       offset 44, size 4
CBO size      48
```

Metal still removes the resource handles from the constant bytes, but the
reflection is straightforward:

```text
Texture0      shaderResource index 0
Texture1      shaderResource index 1
Texture2      shaderResource index 2
Texture3      shaderResource index 3
LinearSampler samplerState index 0
TextureIndex  uniform offset 0, size 4
UvScale       uniform offset 4, size 4
CBO size      8
```

So for small fixed material groups, flattened fields are simpler than
`DescriptorHandle<T>[N]`: DX/VK get a compact stable layout, and Metal gets the
same four resource slots. The tradeoff is shader ergonomics; users need a small
generated accessor or a switch helper when indexing dynamically.

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
5. `Shaders/05_fixed_texture_array.slang`
   - Fragment shader samples from a fixed-size
     `DescriptorHandle<Texture2D<float4>>[4]` array in a constants block.
     This is portable as a reflected fixed resource group; Metal lowers it to a
     native `array<texture2d<...>, 4>`.
6. `Shaders/06_flattened_textures.slang`
   - Fragment shader samples from four named texture handle fields. DXIL and
     SPIR-V both report a compact 48-byte uniform layout, while Metal reports
     four texture resource slots plus an 8-byte POD constant block.

The fourth case is intentionally a stress case. It is natural on DX12/Vulkan,
but Metal lowers the nested texture handles to native texture-resource layout.
Zenith should treat it as a compile-time lowering inspection, not as a portable
Metal runtime binding model. For large material systems, use `uint` material
texture indices plus API-bound texture arrays or argument buffers.

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