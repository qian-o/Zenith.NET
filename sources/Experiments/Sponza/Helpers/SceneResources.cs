using System.Numerics;
using System.Runtime.InteropServices;
using SharpGLTF.Schema2;
using Sponza.Models;
using Zenith.NET;
using Zenith.NET.Extensions.ImageSharp;
using Buffer = Zenith.NET.Buffer;
using Texture = Zenith.NET.Texture;

namespace Sponza.Helpers;

internal unsafe class SceneResources : IDisposable
{
    public readonly SceneData Data;

    public SceneResources(GraphicsContext context)
    {
        ModelRoot model = ModelRoot.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "Sponza.gltf"));
        Dictionary<int, Texture> textures = [];
        MaterialData[] materials = new MaterialData[model.LogicalMaterials.Count];
        MaterialConstants[] materialConstants = new MaterialConstants[materials.Length];

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = model.LogicalMaterials[i];
            MaterialChannel? baseColor = material.FindChannel("BaseColor");
            MaterialChannel? metallicRoughness = material.FindChannel("MetallicRoughness");
            MaterialChannel? normal = material.FindChannel("Normal");

            MaterialData data = new()
            {
                BaseColorFactor = baseColor?.Color ?? Vector4.One,
                MetallicFactor = metallicRoughness?.GetFactor("MetallicFactor") ?? 1.0f,
                RoughnessFactor = metallicRoughness?.GetFactor("RoughnessFactor") ?? 1.0f,
                NormalScale = normal?.GetFactor("NormalScale") ?? 1.0f,
                AlphaCutoff = material.AlphaCutoff,
                DoubleSided = material.DoubleSided,
                AlphaMasked = material.Alpha is AlphaMode.MASK,
                BaseColor = LoadTexture(context, baseColor, textures),
                MetallicRoughness = LoadTexture(context, metallicRoughness, textures),
                Normal = LoadTexture(context, normal, textures)
            };

            materials[i] = data;
            materialConstants[i] = new()
            {
                BaseColorFactor = data.BaseColorFactor,
                MetallicFactor = data.MetallicFactor,
                RoughnessFactor = data.RoughnessFactor,
                NormalScale = data.NormalScale,
                AlphaCutoff = data.AlphaCutoff,
                BaseColor = data.BaseColor?.SampledHandle ?? default,
                MetallicRoughness = data.MetallicRoughness?.SampledHandle ?? default,
                Normal = data.Normal?.SampledHandle ?? default,
                Flags = (data.BaseColor is not null ? 1u : 0u) | (data.MetallicRoughness is not null ? 2u : 0u) | (data.Normal is not null ? 4u : 0u) | (data.AlphaMasked ? 8u : 0u) | (data.DoubleSided ? 16u : 0u),
                Padding = 0
            };
        }

        List<SceneVertex> vertices = [];
        List<ushort> indices = [];
        List<DrawData> draws = [];
        Vector3 boundsMinimum = new(float.MaxValue);
        Vector3 boundsMaximum = new(float.MinValue);

        foreach (Node node in Node.Flatten(model.DefaultScene))
        {
            if (node.Mesh is null)
            {
                continue;
            }

            Matrix4x4 world = node.WorldMatrix;
            Matrix4x4.Invert(world, out Matrix4x4 inverseWorld);
            Matrix4x4 normalWorld = Matrix4x4.Transpose(inverseWorld);
            float worldOrientation = world.GetDeterminant() < 0.0f ? -1.0f : 1.0f;

            foreach (MeshPrimitive primitive in node.Mesh.Primitives)
            {
                IList<Vector3> positions = primitive.GetVertexAccessor("POSITION").AsVector3Array();
                IList<Vector3> normals = primitive.GetVertexAccessor("NORMAL").AsVector3Array();
                IList<Vector4>? tangents = primitive.GetVertexAccessor("TANGENT")?.AsVector4Array();
                IList<Vector2> texCoords = primitive.GetVertexAccessor("TEXCOORD_0").AsVector2Array();
                IList<uint> primitiveIndices = primitive.GetIndices();

                draws.Add(new()
                {
                    IndexCount = (uint)primitiveIndices.Count,
                    FirstIndex = (uint)indices.Count,
                    VertexOffset = vertices.Count,
                    MaterialIndex = (uint)primitive.Material.LogicalIndex,
                    World = world,
                    NormalWorld = normalWorld,
                    WorldOrientation = worldOrientation
                });

                for (int i = 0; i < positions.Count; i++)
                {
                    vertices.Add(new()
                    {
                        Position = positions[i],
                        Normal = normals[i],
                        Tangent = tangents is not null ? tangents[i] : Vector4.Zero,
                        TexCoord = texCoords[i]
                    });

                    Vector3 positionWorld = Vector3.Transform(positions[i], world);
                    boundsMinimum = Vector3.Min(boundsMinimum, positionWorld);
                    boundsMaximum = Vector3.Max(boundsMaximum, positionWorld);
                }

                foreach (uint index in primitiveIndices)
                {
                    indices.Add((ushort)index);
                }
            }
        }

        CommandBuffer commandBuffer = context.TransferQueue.CommandBuffer();
        Buffer vertexBuffer = LoadBuffer(context, commandBuffer, CollectionsMarshal.AsSpan(vertices), BufferUsages.Vertex);
        Buffer indexBuffer = LoadBuffer(context, commandBuffer, CollectionsMarshal.AsSpan(indices), BufferUsages.Index);
        Buffer materialBuffer = LoadBuffer(context, commandBuffer, materialConstants.AsSpan(), BufferUsages.StorageReadOnly);
        commandBuffer.Submit().Wait();

        Data = new()
        {
            VertexBuffer = vertexBuffer,
            IndexBuffer = indexBuffer,
            MaterialBuffer = materialBuffer,
            Textures = [.. textures.Values],
            Draws = [.. draws],
            Materials = materials,
            BoundsMinimumWorld = boundsMinimum,
            BoundsMaximumWorld = boundsMaximum
        };
    }

    public void Dispose()
    {
        Data.MaterialBuffer.Dispose();
        Data.IndexBuffer.Dispose();
        Data.VertexBuffer.Dispose();

        foreach (Texture texture in Data.Textures)
        {
            texture.Dispose();
        }
    }

    private static Texture? LoadTexture(GraphicsContext context, MaterialChannel? channel, Dictionary<int, Texture> textures)
    {
        if (channel?.Texture?.PrimaryImage is not Image image)
        {
            return null;
        }

        if (!textures.TryGetValue(image.LogicalIndex, out Texture? texture))
        {
            using Stream stream = image.Content.Open();
            texture = context.LoadTextureFromStream(stream);
            textures.Add(image.LogicalIndex, texture);
        }

        return texture;
    }

    private static Buffer LoadBuffer<T>(GraphicsContext context, CommandBuffer commandBuffer, Span<T> data, BufferUsages usages) where T : unmanaged
    {
        Buffer buffer = context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(T) * data.Length),
            StrideInBytes = (uint)sizeof(T),
            Usages = usages | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        });

        fixed (T* pointer = data)
        {
            commandBuffer.Upload(buffer, 0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = buffer.Desc.SizeInBytes
            });
        }

        return buffer;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 64)]
file struct MaterialConstants
{
    [FieldOffset(0)]
    public Vector4 BaseColorFactor;

    [FieldOffset(16)]
    public float MetallicFactor;

    [FieldOffset(20)]
    public float RoughnessFactor;

    [FieldOffset(24)]
    public float NormalScale;

    [FieldOffset(28)]
    public float AlphaCutoff;

    [FieldOffset(32)]
    public ResourceHandle BaseColor;

    [FieldOffset(40)]
    public ResourceHandle MetallicRoughness;

    [FieldOffset(48)]
    public ResourceHandle Normal;

    [FieldOffset(56)]
    public uint Flags;

    [FieldOffset(60)]
    public uint Padding;
}
