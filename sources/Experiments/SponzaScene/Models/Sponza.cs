using System.Numerics;
using SharpGLTF.Schema2;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;
using GNode = SharpGLTF.Schema2.Node;

namespace SponzaScene.Models;

internal unsafe class Sponza : DisposableObject
{
    public Sponza()
    {
        ModelRoot root = ModelRoot.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Sponza", "Sponza.gltf"));

        List<Node> nodes = [];
        List<Vertex> vertices = [];
        List<uint> indices = [];
        foreach (GNode node in root.LogicalNodes)
        {
            ProcessNode(node, nodes, vertices, indices);
        }

        AddCube(new(-4.94647f, 1.2f, 1.14748f), 0.2f, (uint)root.LogicalMaterials.Count, nodes, vertices, indices);
        AddCube(new(-4.94647f, 1.2f, -1.75868f), 0.2f, (uint)root.LogicalMaterials.Count, nodes, vertices, indices);
        AddCube(new(3.9f, 1.2f, 1.14748f), 0.2f, (uint)root.LogicalMaterials.Count, nodes, vertices, indices);
        AddCube(new(3.9f, 1.2f, -1.75846f), 0.2f, (uint)root.LogicalMaterials.Count, nodes, vertices, indices);

        Nodes = [.. nodes];

        Materials = [.. root.LogicalMaterials.Select(static material => new Material(material)), new("Emissive", emissiveFactor: new(1.0f, 0.8f, 0.6f, 1.0f), emissiveStrength: 20.0f)];

        Vertices = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Count),
            StrideInBytes = (uint)sizeof(Vertex),
            Flags = BufferUsageFlags.Vertex
        });
        Vertices.Upload([.. vertices], 0);

        Indices = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Count),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.Index
        });
        Indices.Upload([.. indices], 0);
    }

    public DirectionalLight DirectionalLight { get; } = new()
    {
        Direction = Vector3.Normalize(new(0.2f, -1.0f, 0.3f)),
        Color = new(1.0f, 0.95f, 0.85f),
        Intensity = 2.5f
    };

    public PointLight[] PointLights { get; } =
    [
        new()
        {
            Position = new(-4.94647f, 1.2f, 1.14748f),
            Color = new(1.0f, 0.8f, 0.6f),
            Intensity = 20.0f,
            Radius = 10.0f
        },
        new()
        {
            Position = new(-4.94647f, 1.2f, -1.75868f),
            Color = new(1.0f, 0.8f, 0.6f),
            Intensity = 20.0f,
            Radius = 10.0f
        },
        new()
        {
            Position = new(3.9f, 1.2f, 1.14748f),
            Color = new(1.0f, 0.8f, 0.6f),
            Intensity = 20.0f,
            Radius = 10.0f
        },
        new()
        {
            Position = new(3.9f, 1.2f, -1.75846f),
            Color = new(1.0f, 0.8f, 0.6f),
            Intensity = 20.0f,
            Radius = 10.0f
        }
    ];

    public Node[] Nodes { get; }

    public Buffer Vertices { get; }

    public Buffer Indices { get; }

    public Material[] Materials { get; }

    protected override void Destroy()
    {
        foreach (Material material in Materials)
        {
            material.Dispose();
        }

        Indices.Dispose();
        Vertices.Dispose();
    }

    private static void ProcessNode(GNode node, List<Node> nodes, List<Vertex> vertices, List<uint> indices)
    {
        foreach (GNode children in node.VisualChildren)
        {
            ProcessNode(children, nodes, vertices, indices);
        }

        if (node.Mesh is null)
        {
            return;
        }

        foreach (MeshPrimitive primitive in node.Mesh.Primitives)
        {
            IList<Vector3>? positionBuffer = null;
            IList<Vector3>? normalBuffer = null;
            IList<Vector2>? texCoordBuffer = null;
            IList<Vector4>? colorBuffer = null;

            IndirectDrawIndexedArgs args = new()
            {
                IndexCount = (uint)primitive.IndexAccessor.Count,
                InstanceCount = 1,
                FirstIndex = (uint)indices.Count,
                VertexOffset = vertices.Count
            };

            uint vertexCount = 0;

            if (primitive.VertexAccessors.TryGetValue("POSITION", out Accessor? positionAccessor))
            {
                positionBuffer = positionAccessor.AsVector3Array();

                vertexCount = (uint)positionAccessor.Count;
            }

            if (primitive.VertexAccessors.TryGetValue("NORMAL", out Accessor? normalAccessor))
            {
                normalBuffer = normalAccessor.AsVector3Array();
            }

            if (primitive.VertexAccessors.TryGetValue("TEXCOORD_0", out Accessor? texCoordAccessor))
            {
                texCoordBuffer = texCoordAccessor.AsVector2Array();
            }

            if (primitive.VertexAccessors.TryGetValue("COLOR_0", out Accessor? colorAccessor))
            {
                colorBuffer = colorAccessor.AsVector4Array();
            }

            for (uint i = 0; i < vertexCount; i++)
            {
                vertices.Add(new()
                {
                    Position = Vector3.Transform(positionBuffer != null ? positionBuffer[(int)i] : Vector3.Zero, node.WorldMatrix),
                    Normal = Vector3.Normalize(Vector3.TransformNormal(normalBuffer != null ? normalBuffer[(int)i] : Vector3.UnitY, node.WorldMatrix)),
                    TexCoord = texCoordBuffer != null ? texCoordBuffer[(int)i] : Vector2.Zero,
                    Color = colorBuffer != null ? colorBuffer[(int)i] : Vector4.One
                });
            }

            indices.AddRange(primitive.IndexAccessor.AsIndicesArray());

            nodes.Add(new(node.Name, vertexCount, args, (uint)primitive.Material.LogicalIndex));
        }
    }

    private static void AddCube(Vector3 center, float size, uint material, List<Node> nodes, List<Vertex> vertices, List<uint> indices)
    {
        float halfSize = size * 0.5f;
        uint baseIndex = (uint)vertices.Count;
        uint firstIndex = (uint)indices.Count;

        Vector3[] positions =
        [
            new(-halfSize, -halfSize, -halfSize),
            new(halfSize, -halfSize, -halfSize),
            new(halfSize, halfSize, -halfSize),
            new(-halfSize, halfSize, -halfSize),
            new(-halfSize, -halfSize, halfSize),
            new(halfSize, -halfSize, halfSize),
            new(halfSize, halfSize, halfSize),
            new(-halfSize, halfSize, halfSize)
        ];

        Vector3[] normals = [new(0, 0, -1), new(0, 0, 1), new(-1, 0, 0), new(1, 0, 0), new(0, -1, 0), new(0, 1, 0)];

        Vector2[] uvs = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)];

        uint[][] faceVertexIndices = [[0, 3, 2, 1], [4, 5, 6, 7], [4, 7, 3, 0], [1, 2, 6, 5], [4, 0, 1, 5], [3, 7, 6, 2]];

        uint vertexCount = 0;

        for (uint face = 0; face < 6; face++)
        {
            Vector3 normal = normals[face];

            for (int corner = 0; corner < 4; corner++)
            {
                vertices.Add(new()
                {
                    Position = positions[faceVertexIndices[face][corner]] + center,
                    Normal = normal,
                    TexCoord = uvs[corner],
                    Color = Vector4.One
                });
            }

            uint faceBase = face * 4;

            indices.Add(faceBase + 0);
            indices.Add(faceBase + 1);
            indices.Add(faceBase + 2);
            indices.Add(faceBase + 0);
            indices.Add(faceBase + 2);
            indices.Add(faceBase + 3);

            vertexCount += 4;
        }

        IndirectDrawIndexedArgs args = new()
        {
            IndexCount = 36,
            InstanceCount = 1,
            FirstIndex = firstIndex,
            VertexOffset = (int)baseIndex
        };

        nodes.Add(new($"EmissiveCube_{center}", vertexCount, args, material));
    }
}