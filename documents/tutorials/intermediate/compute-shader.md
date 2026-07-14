# Compute Shader

This tutorial generates an animated image with a compute pipeline, stores it in a writable texture, and samples that texture in a fragment shader. The dispatch and draw are recorded into the `GraphicsQueue` command buffer supplied by the shared application.

You will use:

- A bindless `ResourceHandle` represented as a typed Slang `DescriptorHandle<T>`
- A texture with both `Storage` and `Sampled` usages
- A compute pipeline and dimension-based dispatch counts
- `Transition` to move the texture from compute writes to fragment reads
- A graphics pipeline that displays the result on the swap-chain drawable

Start from the [shared application](../getting-started/prerequisites.md). Add the shader and renderer below, then select the renderer in `Program.cs`.

## Shader

Create `Assets/Shaders/ComputeShader.slang`:

```slang
struct Constants
{
    uint Width;

    uint Height;

    float Time;

    float Padding;

    DescriptorHandle<RWTexture2D<float4>> Output;

    DescriptorHandle<Texture2D> Image;

    DescriptorHandle<SamplerState> Sampler;
};

uniform Constants constants;

[shader("compute")]
[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;

    if (pixel.x >= constants.Width || pixel.y >= constants.Height)
    {
        return;
    }

    float2 size = float2(constants.Width, constants.Height);
    float2 uv = (float2(pixel) + 0.5) / size;
    float2 position = (uv * 2.0 - 1.0) * float2(size.x / size.y, 1.0);

    float radius = length(position);
    float phase = radius * 18.0 - constants.Time * 3.0;
    float wave = 0.5 + 0.5 * cos(phase);
    float3 color = 0.5 + 0.5 * cos(float3(0.0, 2.1, 4.2) + phase + wave);

    (*constants.Output)[pixel] = float4(color, 1.0);
}

struct VSOutput
{
    float4 Position : SV_POSITION;

    float2 TexCoord : TEXCOORD0;
};

[shader("vertex")]
VSOutput VSMain(uint vertexID : SV_VertexID)
{
    float2 texCoord = float2((vertexID << 1) & 2, vertexID & 2);

    VSOutput output;
    output.Position = float4(texCoord * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    output.TexCoord = texCoord;

    return output;
}

[shader("fragment")]
float4 FSMain(VSOutput input) : SV_TARGET
{
    return (*constants.Image).Sample(*constants.Sampler, input.TexCoord);
}
```

`DescriptorHandle<T>` gives each 8-byte `ResourceHandle` a resource type in Slang. `Output` dereferences the storage descriptor, while `Image` dereferences a sampled descriptor for the same texture. The two handles are intentionally distinct because they describe different access paths.

The bounds check is required whenever a texture dimension is not an exact multiple of the `16 x 16` thread-group size.

## Renderer

Create `Renderers/ComputeShaderRenderer.cs`:

```csharp
namespace ZenithTutorials.Renderers;

internal unsafe sealed class ComputeShaderRenderer : IRenderer
{
    private const uint ThreadGroupSize = 16;

    private readonly Buffer constantBuffer;
    private readonly Sampler sampler;
    private readonly ComputePipeline computePipeline;
    private readonly GraphicsPipeline displayPipeline;

    private Texture outputTexture = null!;
    private float elapsedTime;

    public ComputeShaderRenderer()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(ComputeConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        using Shader computeShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("ComputeShader.slang"), "CSMain"));
        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("ComputeShader.slang"), "VSMain"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("ComputeShader.slang"), "FSMain"));

        computePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = computeShader });

        displayPipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [App.ColorFormat],
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthNone(),
                Blend = BlendState.Opaque()
            }
        });

        Resize(App.Width, App.Height);
    }

    public void Update(double deltaTime)
    {
        elapsedTime += (float)deltaTime;
    }

    public void Render(CommandBuffer commandBuffer, Texture drawable)
    {
        ComputeConstants constants = new()
        {
            Width = outputTexture.Desc.Width,
            Height = outputTexture.Desc.Height,
            Time = elapsedTime,
            Padding = 0.0f,
            Output = outputTexture.StorageHandle,
            Image = outputTexture.SampledHandle,
            Sampler = sampler.Handle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(ComputeConstants)
        });

        commandBuffer.Transition(outputTexture, default, TextureLayout.Storage);

        commandBuffer.SetPipeline(computePipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);

        uint groupCountX = (outputTexture.Desc.Width + ThreadGroupSize - 1) / ThreadGroupSize;
        uint groupCountY = (outputTexture.Desc.Height + ThreadGroupSize - 1) / ThreadGroupSize;
        commandBuffer.Dispatch(groupCountX, groupCountY, 1);

        commandBuffer.Transition(outputTexture, default, TextureLayout.Sampled);
        commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment);

        commandBuffer.BeginRenderPass([ColorAttachment.Clear(drawable, new(0.02f, 0.025f, 0.035f, 1.0f))], null);

        commandBuffer.SetPipeline(displayPipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Draw(3, 1, 0, 0);

        commandBuffer.EndRenderPass();
    }

    public void Resize(uint width, uint height)
    {
        outputTexture?.Dispose();
        outputTexture = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R32G32B32A32Float,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Storage | TextureUsages.Sampled
        });
    }

    public void Dispose()
    {
        displayPipeline.Dispose();
        computePipeline.Dispose();
        sampler.Dispose();
        constantBuffer.Dispose();
        outputTexture.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 256)]
file struct ComputeConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(8)]
    public float Time;

    [FieldOffset(12)]
    public float Padding;

    [FieldOffset(16)]
    public ResourceHandle Output;

    [FieldOffset(24)]
    public ResourceHandle Image;

    [FieldOffset(32)]
    public ResourceHandle Sampler;
}
```

## Run

Replace `Program.cs` with:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

App.Run<ComputeShaderRenderer>();
```

Run the project:

```bash
dotnet run
```

## How It Works

The output texture has `TextureUsages.Storage | TextureUsages.Sampled`. It starts each frame in its previous `Sampled` layout and follows this sequence:

1. `Transition(..., TextureLayout.Storage)` prepares it for compute writes.
2. `Dispatch` writes one texel per in-range compute invocation.
3. `Transition(..., TextureLayout.Sampled)` makes those writes available to shader reads and changes the texture layout.
4. The fragment shader samples it while rendering to the drawable.

## Synchronization and Lifetime

No additional `Barrier` belongs between steps 2 and 3. A layout-changing `Transition` already carries the texture synchronization required by the RHI. `Barrier` is for dependencies that do not involve a texture layout change, such as the buffer producer/consumer chain in the next tutorial.

The shared application supplies a command buffer from `GraphicsQueue`. That queue can record both the compute dispatch and graphics render pass, so command order and the transition form one dependency chain. A workload submitted separately to `ComputeQueue` would instead need a second command buffer and a timeline wait passed to the consuming submission; this tutorial deliberately keeps both stages in one command buffer.

The constant buffer is `CpuWriteOnly` because the CPU refreshes its dimensions, time, and bindless handles every frame. The generated texture has no CPU mapping path and remains GPU-owned. On resize, the shared shell has already waited for the previous frame before the old texture is disposed and replaced. At shutdown, `App` disposes the renderer before the swap chain and `GraphicsContext`.

## Next Steps

Continue with [Indirect Drawing](indirect-drawing.md) to generate draw arguments on the GPU and synchronize a buffer without changing a texture layout.
