using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Handlers;
using CornellBox.Helpers;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Renderers;

internal unsafe class PathTracingRenderer : Renderer
{
    private const uint ThreadGroupSize = 16;

    private const string ShaderSource = """
        struct Vertex
        {
            private float4 PositionAndPadding;

            private float4 NormalAndMaterialID;

            property float3 Position { get { return PositionAndPadding.xyz; } }

            property float3 Normal { get { return NormalAndMaterialID.xyz; } }

            property uint MaterialID { get { return asuint(NormalAndMaterialID.w); } }
        };

        struct Material
        {
            private float4 AlbedoAndEmission;

            property float3 Albedo { get { return AlbedoAndEmission.xyz; } }

            property float Emission { get { return AlbedoAndEmission.w; } }
        };

        struct CameraParams
        {
            float4x4 InvView;

            float4x4 InvProjection;

            private float4 PositionAndPadding;

            uint FrameCount;

            uint Width;

            uint Height;

            private float padding0;

            property float3 Position { get { return PositionAndPadding.xyz; } }
        };

        RaytracingAccelerationStructure scene;
        ConstantBuffer<CameraParams> camera;
        StructuredBuffer<Vertex> vertices;
        StructuredBuffer<uint> indices;
        StructuredBuffer<Material> materials;
        RWTexture2D<float4> accumTexture;
        RWTexture2D<float4> outputTexture;

        // Light geometry constants (hardcoded ceiling light quad)
        static const float3 LightMin = float3(213.0, 548.6, 227.0);
        static const float3 LightMax = float3(343.0, 548.6, 332.0);
        static const float LightArea = (343.0 - 213.0) * (332.0 - 227.0);
        static const float3 LightNormal = float3(0.0, -1.0, 0.0);

        uint pcgHash(uint input)
        {
            uint state = input * 747796405u + 2891336453u;
            uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
            return (word >> 22u) ^ word;
        }

        float randomFloat(inout uint seed)
        {
            seed = pcgHash(seed);
            return float(seed) / 4294967295.0;
        }

        float3 cosineSampleHemisphere(float3 normal, inout uint seed)
        {
            float r1 = randomFloat(seed);
            float r2 = randomFloat(seed);

            float phi = 2.0 * 3.14159265 * r1;
            float sinTheta = sqrt(r2);
            float cosTheta = sqrt(1.0 - r2);

            float3 w = normal;
            float3 helper = abs(w.x) > 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
            float3 u = normalize(cross(helper, w));
            float3 v = cross(w, u);

            return normalize(u * cos(phi) * sinTheta + v * sin(phi) * sinTheta + w * cosTheta);
        }

        bool traceShadowRay(float3 origin, float3 direction, float maxDist)
        {
            RayDesc shadowRay;
            shadowRay.Origin = origin;
            shadowRay.Direction = direction;
            shadowRay.TMin = 0.001;
            shadowRay.TMax = maxDist - 0.001;

            RayQuery<RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH> shadowQuery;
            shadowQuery.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, shadowRay);

            while (shadowQuery.Proceed()) {}

            return shadowQuery.CommittedStatus() != COMMITTED_NOTHING;
        }

        float3 sampleLightDirect(float3 hitPos, float3 hitNormal, float3 albedo, inout uint rng)
        {
            float u = randomFloat(rng);
            float v = randomFloat(rng);

            float3 lightPoint = float3(
                lerp(LightMin.x, LightMax.x, u),
                LightMin.y,
                lerp(LightMin.z, LightMax.z, v)
            );

            float3 toLight = lightPoint - hitPos;
            float dist = length(toLight);
            float3 L = toLight / dist;

            float NdotL = dot(hitNormal, L);
            if (NdotL <= 0.0)
            {
                return float3(0.0, 0.0, 0.0);
            }

            float lightCosine = max(dot(LightNormal, -L), 0.0);
            if (lightCosine <= 0.0)
            {
                return float3(0.0, 0.0, 0.0);
            }

            if (traceShadowRay(hitPos + hitNormal * 0.001, L, dist))
            {
                return float3(0.0, 0.0, 0.0);
            }

            Material lightMat = materials[3];
            float3 lightEmission = lightMat.Albedo * lightMat.Emission;

            float pdf = 1.0 / LightArea;
            float3 brdf = albedo / 3.14159265;
            float geometryTerm = NdotL * lightCosine / (dist * dist);

            return lightEmission * brdf * geometryTerm / pdf;
        }

        float3 tracePath(float3 origin, float3 direction, inout uint rng)
        {
            float3 throughput = float3(1.0, 1.0, 1.0);
            float3 radiance = float3(0.0, 0.0, 0.0);

            for (int bounce = 0; bounce < 5; bounce++)
            {
                RayDesc ray;
                ray.Origin = origin;
                ray.Direction = direction;
                ray.TMin = 0.001;
                ray.TMax = 100000.0;

                RayQuery<RAY_FLAG_NONE> query;
                query.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, ray);

                while (query.Proceed()) {}

                if (query.CommittedStatus() == COMMITTED_NOTHING)
                {
                    break;
                }

                uint primIdx = query.CommittedPrimitiveIndex();
                float2 bary = query.CommittedTriangleBarycentrics();
                float t = query.CommittedRayT();
                float3 hitPos = origin + direction * t;

                uint i0 = indices[primIdx * 3 + 0];
                uint i1 = indices[primIdx * 3 + 1];
                uint i2 = indices[primIdx * 3 + 2];

                Vertex v0 = vertices[i0];
                Vertex v1 = vertices[i1];
                Vertex v2 = vertices[i2];

                float3 baryWeights = float3(1.0 - bary.x - bary.y, bary.x, bary.y);
                float3 normal = normalize(
                    v0.Normal * baryWeights.x +
                    v1.Normal * baryWeights.y +
                    v2.Normal * baryWeights.z
                );

                if (dot(normal, direction) > 0.0)
                {
                    normal = -normal;
                }

                Material mat = materials[v0.MaterialID];

                if (mat.Emission > 0.0)
                {
                    if (bounce == 0)
                    {
                        radiance += throughput * mat.Albedo * mat.Emission;
                    }

                    break;
                }

                radiance += throughput * sampleLightDirect(hitPos, normal, mat.Albedo, rng);

                float3 newDir = cosineSampleHemisphere(normal, rng);
                throughput *= mat.Albedo;

                origin = hitPos + normal * 0.001;
                direction = newDir;

                if (bounce >= 2)
                {
                    float p = max(throughput.r, max(throughput.g, throughput.b));

                    if (randomFloat(rng) > p)
                    {
                        break;
                    }

                    throughput /= p;
                }
            }

            return radiance;
        }

        [numthreads(16, 16, 1)]
        void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
        {
            uint2 pixel = dispatchThreadID.xy;

            if (pixel.x >= camera.Width || pixel.y >= camera.Height)
            {
                return;
            }

            uint rng = pcgHash(pixel.x + pixel.y * camera.Width + camera.FrameCount * camera.Width * camera.Height);

            float2 jitter = float2(randomFloat(rng), randomFloat(rng));
            float2 uv = (float2(pixel) + jitter) / float2(camera.Width, camera.Height);
            float2 ndc = uv * 2.0 - 1.0;
            ndc.y = -ndc.y;

            float4 target = mul(float4(ndc, 1.0, 1.0), camera.InvProjection);
            float3 localDir = normalize(target.xyz / target.w);
            float3 direction = normalize(mul(float4(localDir, 0.0), camera.InvView).xyz);
            float3 origin = camera.Position;

            float3 color = tracePath(origin, direction, rng);

            float4 prev = accumTexture[pixel];
            float4 accumulated;

            if (camera.FrameCount == 0)
            {
                accumulated = float4(color, 1.0);
            }
            else
            {
                accumulated = prev + float4(color, 1.0);
            }

            accumTexture[pixel] = accumulated;

            float3 avg = accumulated.rgb / float(camera.FrameCount + 1);
            avg = pow(avg, 1.0 / 2.2);
            outputTexture[pixel] = float4(avg, 1.0);
        }
        """;

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer materialBuffer;
    private readonly Buffer cameraBuffer;
    private readonly BottomLevelAccelerationStructure blas;
    private readonly TopLevelAccelerationStructure tlas;
    private readonly ResourceLayout resourceLayout;
    private readonly ComputePipeline pipeline;

    private Texture? accumulationTexture;
    private ResourceTable? resourceTable;

    private Matrix4x4 lastView;
    private Matrix4x4 lastProjection;

    public PathTracingRenderer()
    {
        CornellBoxGeometry.Create(out Vertex[] vertices, out uint[] indices, out Material[] materials);

        vertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length),
            StrideInBytes = (uint)sizeof(Vertex),
            Flags = BufferUsageFlags.ShaderResource | BufferUsageFlags.AccelerationStructure
        });
        vertexBuffer.Upload(vertices, 0);

        indexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Length),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.ShaderResource | BufferUsageFlags.AccelerationStructure
        });
        indexBuffer.Upload(indices, 0);

        materialBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Material) * materials.Length),
            StrideInBytes = (uint)sizeof(Material),
            Flags = BufferUsageFlags.ShaderResource
        });
        materialBuffer.Upload(materials, 0);

        cameraBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(CameraParams),
            StrideInBytes = (uint)sizeof(CameraParams),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        blas = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
        {
            Geometries =
            [
                new()
                {
                    Type = RayTracingGeometryType.Triangles,
                    Triangles = new()
                    {
                        VertexBuffer = vertexBuffer,
                        VertexFormat = PixelFormat.R32G32B32Float,
                        VertexCount = (uint)vertices.Length,
                        VertexStrideInBytes = (uint)sizeof(Vertex),
                        IndexBuffer = indexBuffer,
                        IndexFormat = IndexFormat.UInt32,
                        IndexCount = (uint)indices.Length,
                        Transform = Matrix4x4.Identity
                    },
                    Flags = RayTracingGeometryFlags.Opaque
                }
            ],
            Flags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        tlas = commandBuffer.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
        {
            Instances =
            [
                new()
                {
                    AccelerationStructure = blas,
                    ID = 0,
                    Mask = 0xFF,
                    Transform = Matrix4x4.Identity,
                    Flags = RayTracingInstanceFlags.None
                }
            ],
            Flags = AccelerationStructureBuildFlags.PreferFastTrace
        });

        commandBuffer.Submit(waitForCompletion: true);

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = BindingHelper.Bindings
            (
                new() { Type = ResourceType.AccelerationStructure, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
        });

        using Shader computeShader = App.Context.LoadShaderFromSource(ShaderSource, "CSMain", ShaderStageFlags.Compute);

        pipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = computeShader,
            ResourceLayout = resourceLayout,
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
        });
    }

    public uint FrameCount { get; set; }

    public override void Update(CameraHandler camera)
    {
        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;

        if (view != lastView || projection != lastProjection)
        {
            lastView = view;
            lastProjection = projection;

            FrameCount = 0;
        }

        Matrix4x4.Invert(view, out Matrix4x4 invView);
        Matrix4x4.Invert(projection, out Matrix4x4 invProjection);

        cameraBuffer.Upload<CameraParams>([new()
        {
            InvView = invView,
            InvProjection = invProjection,
            Position = camera.Position,
            FrameCount = FrameCount,
            Width = App.Width,
            Height = App.Height
        }], 0);
    }

    public override void Render(CommandBuffer commandBuffer)
    {
        if (resourceTable is null || accumulationTexture is null)
        {
            accumulationTexture = App.Context.CreateTexture(new()
            {
                Type = TextureType.Texture2D,
                Format = PixelFormat.R32G32B32A32Float,
                Width = App.Width,
                Height = App.Height,
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = SampleCount.Count1,
                Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
            });

            resourceTable = App.Context.CreateResourceTable(new()
            {
                Layout = resourceLayout,
                Resources = [tlas, cameraBuffer, vertexBuffer, indexBuffer, materialBuffer, accumulationTexture, Color]
            });
        }

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetResourceTable(resourceTable);

        commandBuffer.Dispatch((App.Width + ThreadGroupSize - 1) / ThreadGroupSize, (App.Height + ThreadGroupSize - 1) / ThreadGroupSize, 1);

        FrameCount++;
    }

    public override void Resize(uint width, uint height)
    {
        base.Resize(width, height);

        resourceTable?.Dispose();
        resourceTable = null;

        accumulationTexture?.Dispose();
        accumulationTexture = null;

        FrameCount = 0;
    }

    public override void Dispose()
    {
        base.Dispose();

        resourceTable?.Dispose();
        accumulationTexture?.Dispose();

        pipeline.Dispose();
        resourceLayout.Dispose();
        tlas.Dispose();
        blas.Dispose();
        cameraBuffer.Dispose();
        materialBuffer.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 160)]
file struct CameraParams
{
    [FieldOffset(0)]
    public Matrix4x4 InvView;

    [FieldOffset(64)]
    public Matrix4x4 InvProjection;

    [FieldOffset(128)]
    public Vector3 Position;

    [FieldOffset(144)]
    public uint FrameCount;

    [FieldOffset(148)]
    public uint Width;

    [FieldOffset(152)]
    public uint Height;
}
