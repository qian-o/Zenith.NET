using System.Numerics;
using SharpGLTF.Schema2;
using SponzaScene.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;
using GNode = SharpGLTF.Schema2.Node;
using Material = SponzaScene.Models.Material;
using Node = SponzaScene.Models.Node;

namespace SponzaScene.Helpers;

internal unsafe class NewSponza
{
    public const string Directory = @"C:\Users\13247\OneDrive\NewSponza";

    public NewSponza()
    {
        (MainVertices, MainIndices) = LoadModel("NewSponza_Main");
        (IvyGrowthVertices, IvyGrowthIndices) = LoadModel("NewSponza_IvyGrowth");
        (CypressTreeVertices, CypressTreeIndices) = LoadModel("NewSponza_CypressTree");
        (CurtainsVertices, CurtainsIndices) = LoadModel("NewSponza_Curtains");
    }

    public Buffer MainVertices { get; }

    public Buffer MainIndices { get; }

    public Buffer IvyGrowthVertices { get; }

    public Buffer IvyGrowthIndices { get; }

    public Buffer CypressTreeVertices { get; }

    public Buffer CypressTreeIndices { get; }

    public Buffer CurtainsVertices { get; }

    public Buffer CurtainsIndices { get; }

    private static (Buffer Vertices, Buffer Indices) LoadModel(string name)
    {
        ModelRoot root = ModelRoot.Load(Path.Combine(Directory, name, name) + ".gltf");

        Material[] materials = [.. root.LogicalMaterials.Select(static material => new Material(material))];

        List<Node> nodes = [];
        List<Vertex> vertices = [];
        List<uint> indices = [];
        foreach (GNode node in root.LogicalNodes)
        {
            ProcessNode(node, nodes, vertices, indices, materials);
        }

        Buffer vertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Count),
            StrideInBytes = (uint)sizeof(Vertex),
            Flags = BufferUsageFlags.Vertex | BufferUsageFlags.AccelerationStructure
        });
        vertexBuffer.Upload([.. vertices], 0);

        Buffer indexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Count),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.Index | BufferUsageFlags.AccelerationStructure
        });
        indexBuffer.Upload([.. indices], 0);

        return (vertexBuffer, indexBuffer);
    }

    private static void ProcessNode(GNode node, List<Node> nodes, List<Vertex> vertices, List<uint> indices, Material[] materials)
    {
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
                    Position = positionBuffer != null ? positionBuffer[(int)i] : Vector3.Zero,
                    Normal = normalBuffer != null ? normalBuffer[(int)i] : Vector3.Zero,
                    TexCoord = texCoordBuffer != null ? texCoordBuffer[(int)i] : Vector2.Zero,
                    Color = colorBuffer != null ? colorBuffer[(int)i] : Vector4.One
                });
            }

            indices.AddRange(primitive.IndexAccessor.AsIndicesArray());

            nodes.Add(new(node.Name, args, materials[primitive.Material.LogicalIndex]));
        }

        foreach (GNode children in node.VisualChildren)
        {
            ProcessNode(children, nodes, vertices, indices, materials);
        }
    }
}
