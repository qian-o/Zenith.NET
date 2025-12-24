using System.Numerics;
using SharpGLTF.Schema2;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;
using GNode = SharpGLTF.Schema2.Node;

namespace SponzaScene.Models;

internal unsafe class GLTF : DisposableObject
{
    public GLTF(string path)
    {
        Name = Path.GetFileNameWithoutExtension(path);

        ModelRoot root = ModelRoot.Load(path);

        List<Node> nodes = [];
        List<Vertex> vertices = [];
        List<uint> indices = [];
        foreach (GNode node in root.LogicalNodes)
        {
            ProcessNode(node, nodes, vertices, indices);
        }

        Nodes = [.. nodes];

        Vertices = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Count),
            StrideInBytes = (uint)sizeof(Vertex),
            Flags = BufferUsageFlags.Vertex | BufferUsageFlags.AccelerationStructure
        });
        Vertices.Upload([.. vertices], 0);

        Indices = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Count),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.Index | BufferUsageFlags.AccelerationStructure
        });
        Indices.Upload([.. indices], 0);

        Materials = [.. root.LogicalMaterials.Select(static material => new Material(material))];

        //if (App.Context.Capabilities.RayTracingSupported)
        //{
        //    RayTracingGeometry[] geometries = new RayTracingGeometry[Nodes.Length];

        //    for (int i = 0; i < Nodes.Length; i++)
        //    {
        //        Node node = Nodes[i];

        //        geometries[i] = new()
        //        {
        //            Type = RayTracingGeometryType.Triangles,
        //            Triangles = new()
        //            {
        //                VertexBuffer = Vertices,
        //                VertexFormat = PixelFormat.R32G32B32Float,
        //                VertexCount = node.VertexCount,
        //                VertexStrideInBytes = (uint)sizeof(Vertex),
        //                VertexOffsetInBytes = (uint)(sizeof(Vertex) * node.Args.VertexOffset),
        //                IndexBuffer = Indices,
        //                IndexFormat = IndexFormat.UInt32,
        //                IndexCount = node.Args.IndexCount,
        //                IndexOffsetInBytes = sizeof(uint) * node.Args.FirstIndex,
        //                Transform = Matrix4x4.Identity
        //            },
        //            Flags = RayTracingGeometryFlags.Opaque
        //        };
        //    }

        //    CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        //    BLAS = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc()
        //    {
        //        Geometries = geometries,
        //        Flags = AccelerationStructureBuildFlags.PreferFastBuild
        //    });

        //    commandBuffer.Submit();

        //    App.Context.Graphics.WaitIdle();
        //}
    }

    public string Name { get; }

    public Node[] Nodes { get; }

    public Buffer Vertices { get; }

    public Buffer Indices { get; }

    public Material[] Materials { get; }

    public BottomLevelAccelerationStructure? BLAS { get; }

    protected override void Destroy()
    {
        BLAS?.Dispose();

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

            Matrix4x4 localMatrix = node.LocalMatrix;

            for (uint i = 0; i < vertexCount; i++)
            {
                vertices.Add(new()
                {
                    Position = Vector3.Transform(positionBuffer != null ? positionBuffer[(int)i] : Vector3.Zero, localMatrix),
                    Normal = Vector3.Normalize(Vector3.TransformNormal(normalBuffer != null ? normalBuffer[(int)i] : Vector3.UnitY, localMatrix)),
                    TexCoord = texCoordBuffer != null ? texCoordBuffer[(int)i] : Vector2.Zero,
                    Color = colorBuffer != null ? colorBuffer[(int)i] : Vector4.One
                });
            }

            indices.AddRange(primitive.IndexAccessor.AsIndicesArray());

            nodes.Add(new(node.Name, vertexCount, args, (uint)primitive.Material.LogicalIndex));
        }
    }
}
