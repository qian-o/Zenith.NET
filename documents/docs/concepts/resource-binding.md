# Resource Binding

Resources are bound to shaders through `ResourceTable`. Each binding in a table maps to a shader resource declaration (constant buffer, texture, sampler, etc.).

## Binding Model

The binding model has two components:

1. **`ResourceBinding`** — Declares the type and count for a single binding index
2. **`ResourceTable`** — Holds the actual GPU resources, written by index

The `ResourceTable` is created with a `ResourceBinding[]` that must be layout-compatible with the pipeline’s `ResourceBinding[]`. As long as the binding layout matches, the table can be used with the pipeline.

## ResourceBinding

A `ResourceBinding` declares what a single binding index expects:

```csharp
ResourceBinding[] bindings =
[
    new() { Type = ResourceType.ConstantBuffer, Count = 1 },
    new() { Type = ResourceType.Texture, Count = 1 },
    new() { Type = ResourceType.Sampler, Count = 1 }
];
```

| Field | Type | Description |
|-------|------|-------------|
| `Type` | `ResourceType` | The kind of resource at this binding |
| `Count` | `uint` | Number of resources at this binding (use `1` for single resources, `> 1` for arrays) |

### Resource Types

| ResourceType | Shader Declaration | Bindable Types |
|-------------|-------------------|----------------|
| `ConstantBuffer` | `ConstantBuffer<T>` | `Buffer`, `BufferView` |
| `StructuredBuffer` | `StructuredBuffer<T>` | `Buffer`, `BufferView` |
| `StructuredBufferReadWrite` | `RWStructuredBuffer<T>` | `Buffer`, `BufferView` |
| `Texture` | `Texture2D`, `Texture3D`, etc. | `Texture`, `TextureView` |
| `TextureReadWrite` | `RWTexture2D`, etc. | `Texture`, `TextureView` |
| `Sampler` | `SamplerState` | `Sampler` |
| `AccelerationStructure` | `RaytracingAccelerationStructure` | `TopLevelAccelerationStructure` |

## Creating a ResourceTable

```csharp
ResourceTable resourceTable = context.CreateResourceTable(new()
{
    Bindings = bindings
});
```

## Writing Resources

Assign GPU resources to each binding index using `Write()`:

```csharp
resourceTable.Write(0, constantBuffer);
resourceTable.Write(1, texture);
resourceTable.Write(2, sampler);
```

The binding index corresponds to the order in the `ResourceBinding[]` array. Resources can be updated at any time by calling `Write()` again.

### Array Bindings

For bindings with `Count > 1`, pass multiple resources:

```csharp
resourceTable.Write(0, texture0, texture1, texture2);
```

## Connecting Pipeline and Table

The pipeline’s `ResourceBindings` and the resource table’s `Bindings` must have compatible layouts — the same types and counts at each index, but they do not need to be the same array instance:

```csharp
// Pipeline bindings
ResourceBinding[] pipelineBindings =
[
    new() { Type = ResourceType.Texture, Count = 1 },
    new() { Type = ResourceType.Sampler, Count = 1 }
];

pipeline = context.CreateGraphicsPipeline(new()
{
    // ...
    ResourceBindings = pipelineBindings,
    // ...
});

// Table bindings — layout-compatible with the pipeline
ResourceBinding[] tableBindings =
[
    new() { Type = ResourceType.Texture, Count = 1 },
    new() { Type = ResourceType.Sampler, Count = 1 }
];

resourceTable = context.CreateResourceTable(new() { Bindings = tableBindings });
resourceTable.Write(0, texture);
resourceTable.Write(1, sampler);
```

## Binding During Rendering

Set the resource table before draw or dispatch calls:

```csharp
commandBuffer.BeginRenderPass(frameBuffer, clearValue, resourceTable);

commandBuffer.SetPipeline(pipeline);
commandBuffer.PushResourceTable(resourceTable);
commandBuffer.Draw(3, 1, 0, 0);

commandBuffer.EndRenderPass();
```

> [!NOTE]
> Pass the `ResourceTable` to `BeginRenderPass` as well — this allows the backend to perform any necessary resource transitions before rendering begins.

## Shader Binding Order

Shader resource declarations map to binding indices in declaration order. For example:

```hlsl
ConstantBuffer<Constants> constants;  // binding 0
Texture2D albedo;                     // binding 1
SamplerState linearSampler;           // binding 2
```

Corresponds to:

```csharp
ResourceBinding[] bindings =
[
    new() { Type = ResourceType.ConstantBuffer, Count = 1 },  // 0 → constants
    new() { Type = ResourceType.Texture, Count = 1 },         // 1 → albedo
    new() { Type = ResourceType.Sampler, Count = 1 }          // 2 → linearSampler
];
```
