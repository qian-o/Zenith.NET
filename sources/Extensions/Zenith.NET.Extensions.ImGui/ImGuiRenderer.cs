using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;

namespace Zenith.NET.Extensions.ImGui;

internal unsafe class ImGuiRenderer : DisposableObject
{
    public const string Source = """
struct VSInput
{
    float2 Position : POSITION0;

    float2 UV : TEXCOORD0;

    float4 Color : COLOR0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;

    float2 UV : TEXCOORD0;

    float4 Color : COLOR0;
};

struct Constants
{
    float4x4 Projection;

    DescriptorHandle<Texture2D> Texture;

    DescriptorHandle<SamplerState> Sampler;
};

uniform Constants constants;

float3 SrgbToLinear(float3 srgb)
{
    return srgb * (srgb * (srgb * 0.305306011 + 0.682171111) + 0.012522878);
}

[shader("vertex")]
VSOutput VSMain(VSInput input)
{
    VSOutput output;

    output.Position = mul(float4(input.Position, 0.0, 1.0), constants.Projection);
    output.UV = input.UV;
    output.Color = input.Color;

#if 0
    output.Color.rgb = SrgbToLinear(output.Color.rgb);
#endif

    return output;
}

[shader("fragment")]
float4 FSMain(VSOutput input) : SV_TARGET
{
    return input.Color * (*constants.Texture).Sample(*constants.Sampler, input.UV);
}
""";

    private readonly Sampler sampler;
    private readonly GraphicsPipeline graphicsPipeline;
    private readonly Dictionary<Texture, ImTextureID> textureBindings = [];
    private readonly Dictionary<TextureView, ImTextureID> textureViewBindings = [];
    private readonly SortedList<ImTextureID, ResourceHandle> resourceHandleBindings = new(Comparer<ImTextureID>.Create(static (x, y) => x.Handle.CompareTo(y.Handle)));
    private readonly Dictionary<ImTextureID, Texture> drawDataTextures = [];

    private Buffer? vertexBuffer;
    private Buffer? indexBuffer;
    private Buffer? constantBuffer;

    public ImGuiRenderer(GraphicsContext context, AttachmentFormats attachmentFormats, ImGuiColorSpace colorSpace)
    {
        string source = Source.Replace("#if 0", $"#if {(colorSpace is ImGuiColorSpace.Legacy ? 0 : 1)}");

        sampler = context.CreateSampler(SamplerDesc.PointClamp());

        using Shader vertex = context.CreateShader(ZenithCompiler.CompileFromSource(context.GraphicsApi, source, "VSMain"));
        using Shader fragment = context.CreateShader(ZenithCompiler.CompileFromSource(context.GraphicsApi, source, "FSMain"));

        InputLayout inputLayout = new();
        inputLayout.Add(new()
        {
            Format = ElementFormat.Float2,
            Semantic = ElementSemantic.Position
        });

        inputLayout.Add(new()
        {
            Format = ElementFormat.Float2,
            Semantic = ElementSemantic.TexCoord
        });

        inputLayout.Add(new()
        {
            Format = ElementFormat.UByte4UNorm,
            Semantic = ElementSemantic.Color
        });

        graphicsPipeline = context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertex,
            FragmentShader = fragment,
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = attachmentFormats,
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthNone(),
                Blend = BlendState.NonPremultiplied()
            }
        });

        Context = context;
    }

    public GraphicsContext Context { get; }

    public ImTextureID Binding(Texture texture)
    {
        if (!textureBindings.TryGetValue(texture, out ImTextureID textureID))
        {
            ulong id = 0;
            while (resourceHandleBindings.ContainsKey(id))
            {
                id++;
            }

            resourceHandleBindings[textureBindings[texture] = textureID = id] = texture.SampledHandle;
        }

        return textureID;
    }

    public ImTextureID Binding(TextureView textureView)
    {
        if (!textureViewBindings.TryGetValue(textureView, out ImTextureID textureID))
        {
            ulong id = 0;
            while (resourceHandleBindings.ContainsKey(id))
            {
                id++;
            }

            resourceHandleBindings[textureViewBindings[textureView] = textureID = id] = textureView.SampledHandle;
        }

        return textureID;
    }

    public void Render(CommandBuffer commandBuffer, ColorAttachment colorAttachment, ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount is 0)
        {
            return;
        }

        drawData.ScaleClipRects(drawData.FramebufferScale);

        for (int i = 0; i < drawData.Textures.Size; i++)
        {
            ImTextureDataPtr textureData = drawData.Textures[i];

            switch (textureData.Status)
            {
                case ImTextureStatus.WantCreate:
                    {
                        Texture texture = Context.CreateTexture(TextureDesc.Texture2D(textureData.Format is ImTextureFormat.Rgba32 ? PixelFormat.R8G8B8A8UNorm : PixelFormat.R8UNorm,
                                                                                      (uint)textureData.Width,
                                                                                      (uint)textureData.Height,
                                                                                      1,
                                                                                      SampleCount.Count1));

                        Extent3D extent = new()
                        {
                            Width = (uint)textureData.Width,
                            Height = (uint)textureData.Height,
                            Depth = 1
                        };

                        if (textureData.Format is ImTextureFormat.Rgba32)
                        {
                            TextureData data = new()
                            {
                                Pointer = (nint)textureData.Pixels,
                                SizeInBytes = ZenithHelper.SizeInBytes(texture.Desc.Format, extent.Width, extent.Height),
                                RowStrideInBytes = ZenithHelper.RowStrideInBytes(texture.Desc.Format, extent.Width, extent.Height),
                                SliceStrideInBytes = ZenithHelper.SliceStrideInBytes(texture.Desc.Format, extent.Width, extent.Height)
                            };

                            commandBuffer.Upload(texture, default, default, extent, data);
                        }
                        else
                        {
                            TextureData data = new()
                            {
                                Pointer = (nint)textureData.Pixels,
                                SizeInBytes = ZenithHelper.SizeInBytes(texture.Desc.Format, extent.Width, extent.Height),
                                RowStrideInBytes = ZenithHelper.RowStrideInBytes(texture.Desc.Format, extent.Width, extent.Height),
                                SliceStrideInBytes = ZenithHelper.SliceStrideInBytes(texture.Desc.Format, extent.Width, extent.Height)
                            };

                            commandBuffer.Upload(texture, default, default, extent, data);
                        }

                        textureData.SetTexID(Binding(texture));
                        textureData.SetStatus(ImTextureStatus.Ok);

                        drawDataTextures[textureData.TexID] = texture;
                    }
                    break;

                case ImTextureStatus.WantUpdates:
                    {
                        if (drawDataTextures.TryGetValue(textureData.TexID, out Texture? texture))
                        {
                            for (int j = 0; j < textureData.Updates.Size; j++)
                            {
                                ImTextureRect rect = textureData.Updates[j];

                                Offset3D offset = new()
                                {
                                    X = rect.X,
                                    Y = rect.Y,
                                    Z = 0
                                };

                                Extent3D extent = new()
                                {
                                    Width = rect.W,
                                    Height = rect.H,
                                    Depth = 1
                                };

                                using ZenithMarshal.Scope scope = new();

                                if (textureData.Format is ImTextureFormat.Rgba32)
                                {
                                    int* pointer = (int*)ZenithMarshal.Allocate<int>(scope, (uint)(rect.W * rect.H));

                                    for (ushort k = 0; k < rect.H; k++)
                                    {
                                        new ReadOnlySpan<int>(textureData.GetPixelsAt(rect.X, rect.Y + k), rect.W).CopyTo(new(pointer + (k * rect.W), rect.W));
                                    }

                                    TextureData data = new()
                                    {
                                        Pointer = (nint)pointer,
                                        SizeInBytes = (uint)(sizeof(int) * rect.W * rect.H),
                                        RowStrideInBytes = ZenithHelper.RowStrideInBytes(texture.Desc.Format, extent.Width, extent.Height),
                                        SliceStrideInBytes = ZenithHelper.SliceStrideInBytes(texture.Desc.Format, extent.Width, extent.Height)
                                    };

                                    commandBuffer.Upload(texture, default, offset, extent, data);
                                }
                                else
                                {
                                    byte* pointer = (byte*)ZenithMarshal.Allocate<byte>(scope, (uint)(rect.W * rect.H));

                                    for (ushort k = 0; k < rect.H; k++)
                                    {
                                        new ReadOnlySpan<byte>(textureData.GetPixelsAt(rect.X, rect.Y + k), rect.W).CopyTo(new(pointer + (k * rect.W), rect.W));
                                    }

                                    TextureData data = new()
                                    {
                                        Pointer = (nint)pointer,
                                        SizeInBytes = (uint)(rect.W * rect.H),
                                        RowStrideInBytes = ZenithHelper.RowStrideInBytes(texture.Desc.Format, extent.Width, extent.Height),
                                        SliceStrideInBytes = ZenithHelper.SliceStrideInBytes(texture.Desc.Format, extent.Width, extent.Height)
                                    };

                                    commandBuffer.Upload(texture, default, offset, extent, data);
                                }
                            }
                        }

                        textureData.SetStatus(ImTextureStatus.Ok);
                    }
                    break;

                case ImTextureStatus.WantDestroy:
                    {
                        if (drawDataTextures.Remove(textureData.TexID, out Texture? texture))
                        {
                            texture.Dispose();
                        }

                        textureData.SetTexID(null);
                        textureData.SetStatus(ImTextureStatus.Destroyed);
                    }
                    break;
            }
        }

        RemoveDestroyedBindings();

        uint totalVertexSizeInBytes = (uint)(sizeof(ImDrawVert) * (int)(drawData.TotalVtxCount * 1.2));
        if (vertexBuffer is null || vertexBuffer.Desc.SizeInBytes < totalVertexSizeInBytes)
        {
            vertexBuffer?.Dispose();
            vertexBuffer = Context.CreateBuffer(new()
            {
                SizeInBytes = totalVertexSizeInBytes,
                Usages = BufferUsages.Vertex,
                Residency = MemoryResidency.CpuWriteOnly
            });
        }

        uint totalIndexSizeInBytes = (uint)(sizeof(ushort) * (int)(drawData.TotalIdxCount * 1.2));
        if (indexBuffer is null || indexBuffer.Desc.SizeInBytes < totalIndexSizeInBytes)
        {
            indexBuffer?.Dispose();
            indexBuffer = Context.CreateBuffer(new()
            {
                SizeInBytes = totalIndexSizeInBytes,
                Usages = BufferUsages.Index,
                Residency = MemoryResidency.CpuWriteOnly
            });
        }

        uint totalConstantSizeInBytes = (uint)(sizeof(Constants) * (int)(resourceHandleBindings.Count * 1.2));
        if (constantBuffer is null || constantBuffer.Desc.SizeInBytes < totalConstantSizeInBytes)
        {
            constantBuffer?.Dispose();
            constantBuffer = Context.CreateBuffer(new()
            {
                SizeInBytes = totalConstantSizeInBytes,
                Usages = BufferUsages.Constant,
                Residency = MemoryResidency.CpuWriteOnly
            });
        }

        for (int i = 0, vertexOffset = 0, indexOffset = 0; i < drawData.CmdListsCount; i++)
        {
            ImDrawListPtr drawListPtr = drawData.CmdLists[i];

            vertexBuffer.Upload((uint)(sizeof(ImDrawVert) * vertexOffset), new()
            {
                Pointer = (nint)drawListPtr.VtxBuffer.Data,
                SizeInBytes = (uint)(sizeof(ImDrawVert) * drawListPtr.VtxBuffer.Size)
            });

            indexBuffer.Upload((uint)(sizeof(ushort) * indexOffset), new()
            {
                Pointer = (nint)drawListPtr.IdxBuffer.Data,
                SizeInBytes = (uint)(sizeof(ushort) * drawListPtr.IdxBuffer.Size)
            });

            vertexOffset += drawListPtr.VtxBuffer.Size;
            indexOffset += drawListPtr.IdxBuffer.Size;
        }

        Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(drawData.DisplayPos.X,
                                                                     drawData.DisplayPos.X + drawData.DisplaySize.X,
                                                                     drawData.DisplayPos.Y + drawData.DisplaySize.Y,
                                                                     drawData.DisplayPos.Y,
                                                                     0.0f,
                                                                     1.0f);

        Constants[] constants = ArrayPool<Constants>.Shared.Rent(resourceHandleBindings.Count);

        for (int i = 0; i < resourceHandleBindings.Count; i++)
        {
            constants[i] = new()
            {
                Projection = projection,
                Texture = resourceHandleBindings.Values[i],
                Sampler = sampler.Handle
            };
        }

        fixed (Constants* pointer = constants)
        {
            constantBuffer.Upload(0, new()
            {
                Pointer = (nint)pointer,
                SizeInBytes = (uint)(sizeof(Constants) * resourceHandleBindings.Count)
            });
        }

        ArrayPool<Constants>.Shared.Return(constants);

        commandBuffer.BeginDebugEvent("ImGui");

        commandBuffer.BeginRenderPass([colorAttachment], null);

        commandBuffer.SetPipeline(graphicsPipeline);
        commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
        commandBuffer.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt16);

        for (int i = 0, vertexOffset = 0, indexOffset = 0; i < drawData.CmdListsCount; i++)
        {
            ImDrawListPtr drawListPtr = drawData.CmdLists[i];

            for (int j = 0; j < drawListPtr.CmdBuffer.Size; j++)
            {
                ImDrawCmd drawCmd = drawListPtr.CmdBuffer[j];

                if (drawCmd.UserCallback is not null)
                {
                    ImDrawCallback callback = Marshal.GetDelegateForFunctionPointer<ImDrawCallback>((nint)drawCmd.UserCallback);

                    callback(drawListPtr, &drawCmd);
                }
                else
                {
                    Scissor scissor = new()
                    {
                        X = (int)(drawCmd.ClipRect.X - drawData.DisplayPos.X),
                        Y = (int)(drawCmd.ClipRect.Y - drawData.DisplayPos.Y),
                        Width = (uint)(drawCmd.ClipRect.Z - drawCmd.ClipRect.X),
                        Height = (uint)(drawCmd.ClipRect.W - drawCmd.ClipRect.Y)
                    };

                    if (scissor.Width <= 0 || scissor.Height <= 0)
                    {
                        continue;
                    }

                    commandBuffer.SetScissors([scissor]);
                    commandBuffer.SetConstantBuffer(constantBuffer, (uint)(sizeof(Constants) * resourceHandleBindings.IndexOfKey(drawCmd.TexRef.GetTexID())));
                    commandBuffer.DrawIndexed(drawCmd.ElemCount, 1, (uint)(drawCmd.IdxOffset + indexOffset), (int)(drawCmd.VtxOffset + vertexOffset), 0);
                }
            }

            vertexOffset += drawListPtr.VtxBuffer.Size;
            indexOffset += drawListPtr.IdxBuffer.Size;
        }

        commandBuffer.EndRenderPass();

        commandBuffer.EndDebugEvent();
    }

    protected override void Destroy()
    {
        constantBuffer?.Dispose();
        indexBuffer?.Dispose();
        vertexBuffer?.Dispose();

        foreach (Texture texture in drawDataTextures.Values)
        {
            texture.Dispose();
        }
        drawDataTextures.Clear();

        resourceHandleBindings.Clear();

        textureViewBindings.Clear();
        textureBindings.Clear();
        graphicsPipeline.Dispose();
        sampler.Dispose();
    }

    private void RemoveDestroyedBindings()
    {
        ImTextureID[] destroyedTextureIDs = [.. resourceHandleBindings.Keys.Where(textureID =>
        {
            if (textureBindings.FirstOrDefault(kv => kv.Value == textureID).Key is Texture texture)
            {
                return texture.IsDisposed;
            }

            if (textureViewBindings.FirstOrDefault(kv => kv.Value == textureID).Key is TextureView textureView)
            {
                return textureView.IsDisposed;
            }

            return false;
        })];

        foreach (ImTextureID textureID in destroyedTextureIDs)
        {
            resourceHandleBindings.Remove(textureID);

            if (textureViewBindings.FirstOrDefault(kv => kv.Value == textureID).Key is TextureView textureView)
            {
                textureViewBindings.Remove(textureView);
            }

            if (textureBindings.FirstOrDefault(kv => kv.Value == textureID).Key is Texture texture)
            {
                textureBindings.Remove(texture);
            }
        }
    }
}

[StructLayout(LayoutKind.Explicit, Size = 256)]
file struct Constants
{
    [FieldOffset(0)]
    public Matrix4x4 Projection;

    [FieldOffset(64)]
    public ResourceHandle Texture;

    [FieldOffset(72)]
    public ResourceHandle Sampler;
}
