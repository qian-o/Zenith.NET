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

        AddSphere(new(-4.94647f, 1.15f, 1.14748f), 0.1f, (uint)root.LogicalMaterials.Count, nodes, vertices, indices);
        AddSphere(new(-4.94647f, 1.15f, -1.75868f), 0.1f, (uint)root.LogicalMaterials.Count, nodes, vertices, indices);
        AddSphere(new(3.9f, 1.15f, 1.14748f), 0.1f, (uint)root.LogicalMaterials.Count, nodes, vertices, indices);
        AddSphere(new(3.9f, 1.15f, -1.75846f), 0.1f, (uint)root.LogicalMaterials.Count, nodes, vertices, indices);

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
            Position = new(-4.94647f, 1.15f, 1.14748f),
            Color = new(1.0f, 0.8f, 0.6f),
            Intensity = 20.0f,
            Radius = 10.0f
        },
        new()
        {
            Position = new(-4.94647f, 1.15f, -1.75868f),
            Color = new(1.0f, 0.8f, 0.6f),
            Intensity = 20.0f,
            Radius = 10.0f
        },
        new()
        {
            Position = new(3.9f, 1.15f, 1.14748f),
            Color = new(1.0f, 0.8f, 0.6f),
            Intensity = 20.0f,
            Radius = 10.0f
        },
        new()
        {
            Position = new(3.9f, 1.15f, -1.75846f),
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

    private static void AddSphere(Vector3 center,
                                  float radius,
                                  uint material,
                                  List<Node> nodes,
                                  List<Vertex> vertices,
                                  List<uint> indices)
    {
        const uint segments = 16;
        const uint rings = 16;

        uint baseIndex = (uint)vertices.Count;
        uint firstIndex = (uint)indices.Count;
        uint vertexCount = 0;

        for (uint ring = 0; ring <= rings; ring++)
        {
            float phi = MathF.PI * ring / rings;
            float y = MathF.Cos(phi);
            float ringRadius = MathF.Sin(phi);

            for (uint segment = 0; segment <= segments; segment++)
            {
                float theta = 2.0f * MathF.PI * segment / segments;
                float x = ringRadius * MathF.Cos(theta);
                float z = ringRadius * MathF.Sin(theta);

                Vector3 normal = new(x, y, z);
                Vector3 position = (normal * radius) + center;
                Vector2 texCoord = new((float)segment / segments, (float)ring / rings);

                vertices.Add(new()
                {
                    Position = position,
                    Normal = normal,
                    TexCoord = texCoord,
                    Color = Vector4.One
                });

                vertexCount++;
            }
        }

        uint indexCount = 0;
        for (uint ring = 0; ring < rings; ring++)
        {
            for (uint segment = 0; segment < segments; segment++)
            {
                uint current = (ring * (segments + 1)) + segment;
                uint next = current + segments + 1;

                indices.Add(current);
                indices.Add(next);
                indices.Add(current + 1);

                indices.Add(current + 1);
                indices.Add(next);
                indices.Add(next + 1);

                indexCount += 6;
            }
        }

        IndirectDrawIndexedArgs args = new()
        {
            IndexCount = indexCount,
            InstanceCount = 1,
            FirstIndex = firstIndex,
            VertexOffset = (int)baseIndex
        };

        nodes.Add(new($"EmissiveSphere_{center}", vertexCount, args, material));
    }
}