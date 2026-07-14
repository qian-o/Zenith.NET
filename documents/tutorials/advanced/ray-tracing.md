# Ray Tracing

Zenith.NET Ray Tracing combines hardware acceleration structures with inline ray queries. The current RHI builds bottom-level and top-level acceleration structures (BLAS and TLAS), exposes the TLAS through a bindless `ResourceHandle`, and traces it with Slang `RayQuery` from a compute shader. It does not use a separate ray-generation, miss, or hit-group pipeline.

This tutorial builds one indexed triangle, traces it into a storage texture, and samples that texture into the swap-chain drawable. It uses the shared application from [Prerequisites](../getting-started/prerequisites.md), including its `IRenderer.Render(CommandBuffer commandBuffer, Texture drawable)` contract.

> [!NOTE]
> Ray Tracing must be reported by `App.Context.Capabilities.RayTracingSupported`. Support depends on the selected Graphics API, GPU, driver, and Graphics API implementation.

## Data Flow

The workload has a one-time setup phase and a per-frame phase.

Setup:

1. The renderer uploads triangle vertex and index buffers.
2. One compute-queue command buffer builds the BLAS and then the TLAS.
3. The renderer submits that build command buffer and waits for completion.

Per frame:

1. A compute shader uses inline `RayQuery` and writes a storage texture.
2. The texture changes to sampled access and a fullscreen graphics pass writes the drawable through a fragment shader.
3. The shared `App` transitions the drawable to `Present`, submits and waits, then calls `SwapChain.Present()`.

## Shader

Create `Assets/Shaders/RayTracing.slang`:

```slang
struct RayTracingConstants
{
    private uint4 WidthHeightTimeAndPadding;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<RWTexture2D<float4>> OutputTexture;

    DescriptorHandle<Texture2D> Image;

    DescriptorHandle<SamplerState> Sampler;

    property uint Width
    {
        get
        {
            return WidthHeightTimeAndPadding.x;
        }
    }

    property uint Height
    {
        get
        {
            return WidthHeightTimeAndPadding.y;
        }
    }

    property float Time
    {
        get
        {
            return asfloat(WidthHeightTimeAndPadding.z);
        }
    }
};

uniform RayTracingConstants rayTracing;

float3 Sky(float3 direction)
{
    float blend = saturate(direction.y * 0.5 + 0.5);

    return lerp(float3(0.035, 0.045, 0.075), float3(0.24, 0.42, 0.68), blend);
}

[shader("compute")]
[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;

    if (pixel.x >= rayTracing.Width || pixel.y >= rayTracing.Height)
    {
        return;
    }

    float2 uv = (float2(pixel) + 0.5) / float2(rayTracing.Width, rayTracing.Height);
    float2 ndc = uv * 2.0 - 1.0;
    ndc.y = -ndc.y;

    float orbit = rayTracing.Time * 0.35;
    float3 cameraPosition = float3(sin(orbit) * 0.6, 0.15, -3.2);
    float3 cameraTarget = float3(0.0, 0.0, 0.0);
    float3 cameraForward = normalize(cameraTarget - cameraPosition);
    float3 cameraRight = normalize(cross(float3(0.0, 1.0, 0.0), cameraForward));
    float3 cameraUp = cross(cameraForward, cameraRight);

    float aspect = float(rayTracing.Width) / float(rayTracing.Height);
    float tanHalfFov = tan(radians(45.0) * 0.5);
    float3 rayDirection = normalize(cameraForward + ndc.x * aspect * tanHalfFov * cameraRight + ndc.y * tanHalfFov * cameraUp);

    RayDesc ray;
    ray.Origin = cameraPosition;
    ray.Direction = rayDirection;
    ray.TMin = 0.001;
    ray.TMax = 1000.0;

    RayQuery<RAY_FLAG_NONE> query;
    query.TraceRayInline(*rayTracing.Scene, RAY_FLAG_NONE, 0xFF, ray);

    while (query.Proceed())
    {
    }

    float3 color = Sky(rayDirection);

    if (query.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
    {
        float2 barycentrics = query.CommittedTriangleBarycentrics();
        float3 weights = float3(1.0 - barycentrics.x - barycentrics.y, barycentrics.x, barycentrics.y);

        color = float3(1.0, 0.16, 0.08) * weights.x + float3(0.10, 0.92, 0.34) * weights.y + float3(0.12, 0.36, 1.0) * weights.z;
    }

    (*rayTracing.OutputTexture)[pixel] = float4(color, 1.0);
}

struct DisplayOutput
{
    float4 Position : SV_POSITION;

    float2 TexCoord : TEXCOORD0;
};

[shader("vertex")]
DisplayOutput VSMain(uint vertexID : SV_VertexID)
{
    float2 texCoord = float2((vertexID << 1) & 2, vertexID & 2);

    DisplayOutput output;
    output.Position = float4(texCoord * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    output.TexCoord = texCoord;

    return output;
}

[shader("fragment")]
float4 FSMain(DisplayOutput input) : SV_TARGET
{
    return (*rayTracing.Image).Sample(*rayTracing.Sampler, input.TexCoord);
}
```

`DescriptorHandle<T>` is Slang's bindless view of a Zenith.NET `ResourceHandle`. Dereferencing `Scene` gives `TraceRayInline` the TLAS. The same texture has separate storage and sampled handles because those handles describe different access paths.

## Renderer

Create `Renderers/RayTracingRenderer.cs`:

```csharp
namespace ZenithTutorials.Renderers;

internal unsafe sealed class RayTracingRenderer : IRenderer
{
    private const uint ThreadGroupSize = 16;

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer constantBuffer;
    private readonly Sampler sampler;
    private readonly ComputePipeline rayTracingPipeline;
    private readonly GraphicsPipeline displayPipeline;

    private readonly BottomLevelAccelerationStructure blas;
    private readonly TopLevelAccelerationStructure tlas;

    private Texture outputTexture;
    private float totalTime;

    public RayTracingRenderer()
    {
        if (!App.Context.Capabilities.RayTracingSupported)
        {
            throw new NotSupportedException(
                $"Ray Tracing is not supported by '{App.Context.Capabilities.DeviceName}' " +
                $"through the {App.Context.GraphicsApi} Graphics API.");
        }

        Vector3[] vertices =
        [
            new(-1.35f, -1.0f, 0.0f),
            new( 0.0f,   1.25f, 0.0f),
            new( 1.35f, -1.0f, 0.0f)
        ];

        uint[] indices = [0, 1, 2];

        vertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vector3) * vertices.Length),
            StrideInBytes = (uint)sizeof(Vector3),
            Usages = BufferUsages.StorageReadOnly | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        });

        fixed (Vector3* pointer = vertices)
        {
            vertexBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(Vector3) * vertices.Length)
            });
        }

        indexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Length),
            StrideInBytes = sizeof(uint),
            Usages = BufferUsages.StorageReadOnly | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        });

        fixed (uint* pointer = indices)
        {
            indexBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(uint) * indices.Length)
            });
        }

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(RayTracingConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        using Shader computeShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("RayTracing.slang"), "CSMain"));
        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("RayTracing.slang"), "VSMain"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, App.ShaderPath("RayTracing.slang"), "FSMain"));

        rayTracingPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = computeShader });

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

        CommandBuffer commandBuffer = App.Context.ComputeQueue.CommandBuffer();

        blas = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
        {
            Geometries =
            [
                new()
                {
                    Type = RayTracingGeometryType.Triangle,
                    TriangleGeometry = new()
                    {
                        VertexBuffer = vertexBuffer,
                        VertexFormat = PixelFormat.R32G32B32Float,
                        VertexCount = (uint)vertices.Length,
                        VertexStrideInBytes = (uint)sizeof(Vector3),
                        IndexBuffer = indexBuffer,
                        IndexFormat = IndexFormat.UInt32,
                        IndexCount = (uint)indices.Length,
                        Transform = Matrix4x4.Identity
                    },
                    IsOpaque = true
                }
            ],
            BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        tlas = commandBuffer.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
        {
            Instances =
            [
                new()
                {
                    AccelerationStructure = blas,
                    InstanceId = 0,
                    VisibilityMask = 0xFF,
                    Transform = Matrix4x4.Identity,
                    Flags = RayTracingInstanceFlags.None
                }
            ],
            BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        commandBuffer.Submit().Wait();

        outputTexture = CreateOutputTexture(App.Width, App.Height);
    }

    public void Update(double deltaTime)
    {
        totalTime += (float)deltaTime;
    }

    public void Render(CommandBuffer commandBuffer, Texture drawable)
    {
        RayTracingConstants constants = new()
        {
            Width = App.Width,
            Height = App.Height,
            Time = totalTime,
            Scene = tlas.Handle,
            OutputTexture = outputTexture.StorageHandle,
            Image = outputTexture.SampledHandle,
            Sampler = sampler.Handle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(RayTracingConstants)
        });

        commandBuffer.Transition(outputTexture, default, TextureLayout.Storage);
        commandBuffer.SetPipeline(rayTracingPipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Dispatch((App.Width + ThreadGroupSize - 1) / ThreadGroupSize, (App.Height + ThreadGroupSize - 1) / ThreadGroupSize, 1);

        commandBuffer.Transition(outputTexture, default, TextureLayout.Sampled);
        commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(drawable)], null);

        commandBuffer.SetPipeline(displayPipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Draw(3, 1, 0, 0);

        commandBuffer.EndRenderPass();
    }

    public void Resize(uint width, uint height)
    {
        Texture replacement = CreateOutputTexture(width, height);

        outputTexture.Dispose();
        outputTexture = replacement;
    }

    public void Dispose()
    {
        outputTexture.Dispose();
        displayPipeline.Dispose();
        rayTracingPipeline.Dispose();
        sampler.Dispose();
        constantBuffer.Dispose();

        tlas.Dispose();
        blas.Dispose();

        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }

    private static Texture CreateOutputTexture(uint width, uint height)
    {
        return App.Context.CreateTexture(new()
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
}

[StructLayout(LayoutKind.Explicit, Size = 48)]
file struct RayTracingConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(8)]
    public float Time;

    [FieldOffset(12)]
    public uint Padding;

    [FieldOffset(16)]
    public ResourceHandle Scene;

    [FieldOffset(24)]
    public ResourceHandle OutputTexture;

    [FieldOffset(32)]
    public ResourceHandle Image;

    [FieldOffset(40)]
    public ResourceHandle Sampler;
}
```

## Run

The shared `App.Run<TRenderer>()` constructs the renderer before registering frame callbacks. If construction throws, its `finally` block still disposes the swap chain, window, and graphics context. Catch the capability exception at the entry point so an unsupported device receives a concise message instead of only an unhandled exception stack.

Replace `Program.cs` with:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

try
{
    App.Run<RayTracingRenderer>();
}
catch (NotSupportedException exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
}
```

This is a runtime check against the actual context and selected Graphics API. A platform-name or GPU-family check would not reliably describe the enabled Graphics API features.

Run the tutorial application:

```bash
dotnet run
```

On a supported device, the window shows a slowly orbiting, barycentrically colored triangle over a sky gradient. On an unsupported device, the process prints the device name and selected Graphics API, then exits with a nonzero code.

## How It Works

The explicit C# layout matches the Slang struct byte for byte. The first 16 bytes correspond to `uint4 WidthHeightTimeAndPadding`; `Time` occupies the third 32-bit lane and Slang recovers it with `asfloat`. Each `ResourceHandle` occupies 8 bytes, so the four descriptors begin at offsets 16, 24, 32, and 40. The full constant block is 48 bytes.

The intermediate output uses `R32G32B32A32Float`, matching the storage-texture path used by the compute tutorial. The display pipeline samples that texture and converts the result into `App.ColorFormat` when it writes the swap-chain drawable.

## Synchronization and Lifetime

### Synchronization

The vertex and index `Upload` calls use Zenith.NET's transfer queue and wait before returning. The BLAS and TLAS builds are then recorded in that order into one compute-queue command buffer, matching the repository's path tracing renderer. Queue order makes the BLAS available to the following TLAS build, and `commandBuffer.Submit().Wait()` completes both builds before any graphics-queue command buffer can trace the TLAS.

Per-frame Ray Tracing and display work share the graphics-queue command buffer supplied by `App`. The output texture transitions to `Storage` before `Dispatch`, then to `Sampled` before the fragment shader reads it. That layout-changing transition carries the compute-write to fragment-read dependency, so no redundant global barrier is needed. The drawable remains in `ColorAttachment` when `Render` returns; the shared `App` owns its final `Present` transition, submission wait, and presentation.

### Resize and Lifetime

`Resize` creates the replacement texture before disposing the old one. The next `Render` call writes the replacement's storage and sampled handles into the constant buffer, so no stale bindless handle survives a resize. This is safe with the shared synchronous frame loop because the preceding frame submission has completed before resize work runs.

Disposal follows resource dependencies in reverse: the output and pipelines go first, then the sampler and constant buffer, TLAS, BLAS, and finally the geometry buffers retained by the BLAS. The shared application disposes the renderer before disposing `App.Context`.

## Next Steps

- Add more triangle geometries to the BLAS and use `CommittedPrimitiveIndex()` to select material data.
- Add additional BLAS instances with different transforms and visibility masks to the TLAS.
- Add recursive lighting by issuing more inline `RayQuery` operations from the compute shader.
- Continue with [Mesh Shading](mesh-shading.md).
