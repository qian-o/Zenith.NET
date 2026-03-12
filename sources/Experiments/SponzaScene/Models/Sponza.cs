using System.Numerics;
using Hexa.NET.ImGui;
using SharpGLTF.Schema2;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;
using GNode = SharpGLTF.Schema2.Node;

namespace SponzaScene.Models;

internal unsafe class Sponza : DisposableObject
{
    private static readonly Vector3 HorizonColor = new(1.0f, 0.65f, 0.4f);
    private static readonly Vector3 DayColor = new(1.0f, 0.9f, 0.75f);

    private float directionalLightProgress = 0.454f;

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

        uint baseMaterialIndex = (uint)root.LogicalMaterials.Count;

        AddSphere(new(-29.67882f, 6.9f, 6.88488f), 0.6f, baseMaterialIndex, nodes, vertices, indices);
        AddSphere(new(-29.67882f, 6.9f, -10.55208f), 0.6f, baseMaterialIndex + 1, nodes, vertices, indices);
        AddSphere(new(23.4f, 6.9f, 6.88488f), 0.6f, baseMaterialIndex + 2, nodes, vertices, indices);
        AddSphere(new(23.4f, 6.9f, -10.55076f), 0.6f, baseMaterialIndex + 3, nodes, vertices, indices);

        Nodes = [.. nodes];

        Materials =
        [
            .. root.LogicalMaterials.Select(static material => new Material(material)),
            new("Emissive_Cyan", emissiveFactor: new(0.3f, 0.9f, 1.0f, 1.0f), emissiveStrength: 20.0f),
            new("Emissive_Magenta", emissiveFactor: new(1.0f, 0.3f, 0.8f, 1.0f), emissiveStrength: 20.0f),
            new("Emissive_Yellow", emissiveFactor: new(1.0f, 0.9f, 0.2f, 1.0f), emissiveStrength: 20.0f),
            new("Emissive_Green", emissiveFactor: new(0.3f, 1.0f, 0.4f, 1.0f), emissiveStrength: 20.0f)
        ];

        Vertices = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Count),
            StrideInBytes = (uint)sizeof(Vertex),
            Flags = BufferUsageFlags.Vertex | (App.Context.Capabilities.RayTracingSupported ? BufferUsageFlags.AccelerationStructure : 0)
        });
        Vertices.Upload([.. vertices], 0);

        Indices = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Count),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.Index | (App.Context.Capabilities.RayTracingSupported ? BufferUsageFlags.AccelerationStructure : 0)
        });
        Indices.Upload([.. indices], 0);

        if (App.Context.Capabilities.RayTracingSupported)
        {
            RayTracingGeometry[] geometries = new RayTracingGeometry[Nodes.Length];
            for (int i = 0; i < Nodes.Length; i++)
            {
                Node node = Nodes[i];

                geometries[i] = new()
                {
                    Type = RayTracingGeometryType.Triangles,
                    Triangles = new()
                    {
                        VertexBuffer = Vertices,
                        VertexFormat = PixelFormat.R32G32B32Float,
                        VertexCount = node.VertexCount,
                        VertexStrideInBytes = (uint)sizeof(Vertex),
                        VertexOffsetInBytes = (uint)(sizeof(Vertex) * node.Args.VertexOffset),
                        IndexBuffer = Indices,
                        IndexFormat = IndexFormat.UInt32,
                        IndexCount = node.Args.IndexCount,
                        IndexOffsetInBytes = sizeof(uint) * node.Args.FirstIndex,
                        Transform = Matrix4x4.Identity
                    },
                    Flags = RayTracingGeometryFlags.Opaque
                };
            }

            CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

            BLAS = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc()
            {
                Geometries = geometries,
                Flags = AccelerationStructureBuildFlags.PreferFastTrace
            });

            RayTracingInstance instance = new()
            {
                AccelerationStructure = BLAS,
                InstanceMask = 0xFF,
                Transform = Matrix4x4.Identity,
                Flags = RayTracingInstanceFlags.TriangleCullDisable
            };

            TLAS = commandBuffer.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc()
            {
                Instances = [instance],
                Flags = AccelerationStructureBuildFlags.PreferFastTrace
            });

            commandBuffer.Submit(true);
        }
    }

    public DirectionalLight DirectionalLight
    {
        get
        {
            const float maxElevation = 75.0f;
            const float degToRad = MathF.PI / 180.0f;

            float progressAngle = directionalLightProgress * MathF.PI;
            float elevation = maxElevation * MathF.Sin(progressAngle);
            float elevationRad = elevation * degToRad;

            Vector3 direction = Vector3.Normalize(new(0.0f, -MathF.Sin(elevationRad), -MathF.Cos(progressAngle)));

            float colorFactor = Math.Clamp(elevation / maxElevation, 0.0f, 1.0f);
            float smoothColorFactor = colorFactor * colorFactor * (3.0f - (2.0f * colorFactor));
            float intensityFactor = MathF.Sin(colorFactor * MathF.PI * 0.5f);

            return new()
            {
                DirectionAndIntensity = new(direction, float.Lerp(2.0f, 5.0f, intensityFactor)),
                ColorAndPadding = new(Vector3.Lerp(HorizonColor, DayColor, smoothColorFactor), 0.0f)
            };
        }
    }

    public PointLight[] PointLights { get; } =
    [
        new()
        {
            PositionAndRadius = new(-29.67882f, 6.9f, 6.88488f, 30.0f),
            ColorAndIntensity = new(0.3f, 0.9f, 1.0f, 800.0f)
        },
        new()
        {
            PositionAndRadius = new(-29.67882f, 6.9f, -10.55208f, 30.0f),
            ColorAndIntensity = new(1.0f, 0.3f, 0.8f, 800.0f)
        },
        new()
        {
            PositionAndRadius = new(23.4f, 6.9f, 6.88488f, 30.0f),
            ColorAndIntensity = new(1.0f, 0.9f, 0.2f, 800.0f)
        },
        new()
        {
            PositionAndRadius = new(23.4f, 6.9f, -10.55076f, 30.0f),
            ColorAndIntensity = new(0.3f, 1.0f, 0.4f, 800.0f)
        }
    ];

    public Node[] Nodes { get; }

    public Material[] Materials { get; }

    public Buffer Vertices { get; }

    public Buffer Indices { get; }

    public BottomLevelAccelerationStructure? BLAS { get; }

    public TopLevelAccelerationStructure? TLAS { get; }

    public void UI()
    {
        ImGui.SliderFloat("Time of Day", ref directionalLightProgress, 0.0f, 1.0f);
    }

    protected override void Destroy()
    {
        foreach (Material material in Materials)
        {
            material.Dispose();
        }

        TLAS?.Dispose();
        BLAS?.Dispose();
        Indices.Dispose();
        Vertices.Dispose();
    }

    private static void ProcessNode(GNode node, List<Node> nodes, List<Vertex> vertices, List<uint> indices)
    {
        Matrix4x4 worldMatrix = node.WorldMatrix * Matrix4x4.CreateScale(6.0f);

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
            IndirectDrawIndexedArgs args = new()
            {
                IndexCount = (uint)primitive.IndexAccessor.Count,
                InstanceCount = 1,
                FirstIndex = (uint)indices.Count,
                VertexOffset = vertices.Count
            };

            primitive.VertexAccessors.TryGetValue("POSITION", out Accessor? positionAccessor);
            primitive.VertexAccessors.TryGetValue("NORMAL", out Accessor? normalAccessor);
            primitive.VertexAccessors.TryGetValue("TEXCOORD_0", out Accessor? texCoordAccessor);
            primitive.VertexAccessors.TryGetValue("COLOR_0", out Accessor? colorAccessor);

            IList<Vector3>? positions = positionAccessor?.AsVector3Array();
            IList<Vector3>? normals = normalAccessor?.AsVector3Array();
            IList<Vector2>? texCoords = texCoordAccessor?.AsVector2Array();
            IList<Vector4>? colors = colorAccessor?.AsVector4Array();

            uint vertexCount = (uint)(positionAccessor?.Count ?? 0);

            for (int i = 0; i < vertexCount; i++)
            {
                vertices.Add(new()
                {
                    Position = Vector3.Transform(positions?[i] ?? Vector3.Zero, worldMatrix),
                    Normal = Vector3.Normalize(Vector3.TransformNormal(normals?[i] ?? Vector3.UnitY, worldMatrix)),
                    TexCoord = texCoords?[i] ?? Vector2.Zero,
                    Color = colors?[i] ?? Vector4.One
                });
            }

            indices.AddRange(primitive.IndexAccessor.AsIndicesArray());
            nodes.Add(new(node.Name, vertexCount, args, (uint)primitive.Material.LogicalIndex));
        }
    }

    private static void AddSphere(Vector3 center, float radius, uint material, List<Node> nodes, List<Vertex> vertices, List<uint> indices)
    {
        const uint segments = 16;
        const uint rings = 16;

        uint baseIndex = (uint)vertices.Count;
        uint firstIndex = (uint)indices.Count;

        for (uint ring = 0; ring <= rings; ring++)
        {
            float phi = MathF.PI * ring / rings;
            float y = MathF.Cos(phi);
            float ringRadius = MathF.Sin(phi);

            for (uint segment = 0; segment <= segments; segment++)
            {
                float theta = 2.0f * MathF.PI * segment / segments;
                Vector3 normal = new(ringRadius * MathF.Cos(theta), y, ringRadius * MathF.Sin(theta));

                vertices.Add(new()
                {
                    Position = (normal * radius) + center,
                    Normal = normal,
                    TexCoord = new((float)segment / segments, (float)ring / rings),
                    Color = Vector4.One
                });
            }
        }

        for (uint ring = 0; ring < rings; ring++)
        {
            for (uint segment = 0; segment < segments; segment++)
            {
                uint current = (ring * (segments + 1)) + segment;
                uint next = current + segments + 1;

                indices.AddRange([current, next, current + 1, current + 1, next, next + 1]);
            }
        }

        nodes.Add(new($"EmissiveSphere_{center}", (rings + 1) * (segments + 1), new()
        {
            IndexCount = rings * segments * 6,
            InstanceCount = 1,
            FirstIndex = firstIndex,
            VertexOffset = (int)baseIndex
        }, material));
    }
}