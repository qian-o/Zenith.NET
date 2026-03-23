using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCommandEncoder(MTLGraphicsContext context, MTL4CommandBuffer commandBuffer, MTL4ArgumentTable argumentTable) : GraphicsResource(context)
{
    private readonly List<nuint> queryBuffers = [];
    private readonly Dictionary<uint, nuint> vertexBuffers = [];

    private Scissor[]? todoScissors;
    private Viewport[]? todoViewports;
    private Pipeline? currentPipeline;
    private ResourceTable? currentResourceTable;
    private bool needsRebind;

    public MTL4RenderCommandEncoder? Render { get; private set; }

    public MTL4ComputeCommandEncoder? Compute { get; private set; }

    public nuint IndexBuffer { get; private set; }

    public uint IndexSizeInBytes { get; private set; }

    public uint IndexStrideInBytes { get; private set; }

    public MTLIndexType IndexType { get; private set; }

    public MTLPrimitiveType PrimitiveType { get; private set; }

    public MTLSize ThreadGroupSize { get; private set; }

    public MTLSize AmplificationThreadGroupSize { get; private set; }

    public MTLSize MeshThreadGroupSize { get; private set; }

    public void Begin()
    {
        Compute = commandBuffer.MakeComputeCommandEncoder();
    }

    public void End()
    {
        EndRender();
        EndCompute();

        todoScissors = null;
        todoViewports = null;
        currentPipeline = null;
        currentResourceTable = null;
        needsRebind = false;

        queryBuffers.Clear();
        vertexBuffers.Clear();
    }

    public void BeginRenderPass(MTL4RenderPassDescriptor descriptor)
    {
        EndCompute();

        Render = commandBuffer.MakeRenderCommandEncoder(descriptor);

        if (todoScissors is not null)
        {
            SetScissors(todoScissors);

            todoScissors = null;
        }

        if (todoViewports is not null)
        {
            SetViewports(todoViewports);

            todoViewports = null;
        }
    }

    public void EndRenderPass()
    {
        EndRender();

        Compute = commandBuffer.MakeComputeCommandEncoder();
    }

    public void SetScissors(Scissor[] scissors)
    {
        if (Render is null)
        {
            todoScissors = [.. scissors];
        }
        else
        {
            MTLScissorRect[] mtlScissors = [.. scissors.Select(static item => new MTLScissorRect((uint)item.X, (uint)item.Y, item.Width, item.Height))];

            Render.SetScissorRects(mtlScissors);
        }
    }

    public void SetViewports(Viewport[] viewports)
    {
        if (Render is null)
        {
            todoViewports = [.. viewports];
        }
        else
        {
            MTLViewport[] mtlViewports = [.. viewports.Select(static item => new MTLViewport(item.X, item.Y, item.Width, item.Height, item.MinDepth, item.MaxDepth))];

            Render.SetViewports(mtlViewports);
        }
    }

    public void SetPipeline(GraphicsPipeline pipeline)
    {
        currentPipeline = pipeline;

        needsRebind = true;

        PrimitiveType = MTLFormats.Metal(pipeline.Desc.PrimitiveTopology).Type;
    }

    public void SetPipeline(ComputePipeline pipeline)
    {
        currentPipeline = pipeline;

        needsRebind = true;

        ThreadGroupSize = new(pipeline.Desc.ThreadGroupSizeX, pipeline.Desc.ThreadGroupSizeY, pipeline.Desc.ThreadGroupSizeZ);
    }

    public void SetPipeline(MeshShadingPipeline pipeline)
    {
        currentPipeline = pipeline;

        needsRebind = true;

        AmplificationThreadGroupSize = new(pipeline.Desc.AmplificationThreadGroupSizeX, pipeline.Desc.AmplificationThreadGroupSizeY, pipeline.Desc.AmplificationThreadGroupSizeZ);
        MeshThreadGroupSize = new(pipeline.Desc.MeshThreadGroupSizeX, pipeline.Desc.MeshThreadGroupSizeY, pipeline.Desc.MeshThreadGroupSizeZ);
    }

    public void SetVertexBuffer(Buffer buffer, uint offsetInBytes, uint index)
    {
        vertexBuffers[index] = buffer.Metal().GpuAddress + offsetInBytes;

        needsRebind = true;
    }

    public void SetIndexBuffer(Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        IndexBuffer = buffer.Metal().GpuAddress + offsetInBytes;
        IndexSizeInBytes = buffer.Desc.SizeInBytes - offsetInBytes;
        IndexStrideInBytes = (uint)(format is IndexFormat.UInt16 ? sizeof(ushort) : sizeof(uint));
        IndexType = MTLFormats.Metal(format);
    }

    public void SetResourceTable(ResourceTable resourceTable)
    {
        currentResourceTable = resourceTable;

        needsRebind = true;
    }

    public void Bind()
    {
        if (!needsRebind)
        {
            return;
        }

        switch (currentPipeline)
        {
            case MTLGraphicsPipeline graphicsPipeline:
                {
                    BindRenderPipeline(graphicsPipeline.Desc.RenderStates, graphicsPipeline.RenderPipelineState, graphicsPipeline.DepthStencilState);

                    if (currentResourceTable is MTLResourceTable resourceTable)
                    {
                        Render?.SetArgumentTable(resourceTable.ArgumentTable, MTLRenderStages.Fragment);

                        resourceTable.Bind(argumentTable);
                    }

                    foreach (KeyValuePair<uint, nuint> vertexBuffer in vertexBuffers)
                    {
                        argumentTable.SetAddress(vertexBuffer.Value, graphicsPipeline.VertexBufferStartIndex + vertexBuffer.Key);
                    }

                    Render?.SetArgumentTable(argumentTable, MTLRenderStages.Vertex);
                }
                break;

            case MTLComputePipeline computePipeline:
                {
                    Compute?.SetComputePipelineState(computePipeline.ComputePipelineState);

                    if (currentResourceTable is MTLResourceTable resourceTable)
                    {
                        Compute?.SetArgumentTable(resourceTable.ArgumentTable);
                    }
                }
                break;

            case MTLMeshShadingPipeline meshShadingPipeline:
                {
                    BindRenderPipeline(meshShadingPipeline.Desc.RenderStates, meshShadingPipeline.RenderPipelineState, meshShadingPipeline.DepthStencilState);

                    if (currentResourceTable is MTLResourceTable resourceTable)
                    {
                        Render?.SetArgumentTable(resourceTable.ArgumentTable, MTLRenderStages.Object | MTLRenderStages.Mesh);
                    }
                }
                break;
        }

        needsRebind = false;
    }

    public void BeginDebugEvent(string label)
    {
        Render?.PushDebugGroup(label);
        Compute?.PushDebugGroup(label);
    }

    public void EndDebugEvent()
    {
        Render?.PopDebugGroup();
        Compute?.PopDebugGroup();
    }

    public void InsertDebugMarker(string label)
    {
        Render?.InsertDebugSignpost(label);
        Compute?.InsertDebugSignpost(label);
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Render?.Dispose();
        Render = null;

        Compute?.Dispose();
        Compute = null;
    }

    private void EndRender()
    {
        Render?.EndEncoding();
        Render?.Dispose();
        Render = null;
    }

    private void EndCompute()
    {
        Compute?.EndEncoding();
        Compute?.Dispose();
        Compute = null;
    }

    private void BindRenderPipeline(RenderStates renderStates, MTLRenderPipelineState renderPipelineState, MTLDepthStencilState depthStencilState)
    {
        Render?.SetRenderPipelineState(renderPipelineState);

        Render?.SetCullMode(MTLFormats.Metal(renderStates.RasterizerState.CullMode));

        Render?.SetDepthClipMode(renderStates.RasterizerState.DepthClipEnable ? MTLDepthClipMode.Clip : MTLDepthClipMode.Clamp);
        Render?.SetDepthBias(renderStates.RasterizerState.DepthBias, renderStates.RasterizerState.SlopeScaledDepthBias, renderStates.RasterizerState.DepthBiasClamp);

        Render?.SetTriangleFillMode(MTLFormats.Metal(renderStates.RasterizerState.FillMode));

        if (renderStates.BlendFactor.HasValue)
        {
            Render?.SetBlendColor(renderStates.BlendFactor.Value.X, renderStates.BlendFactor.Value.Y, renderStates.BlendFactor.Value.Z, renderStates.BlendFactor.Value.W);
        }

        Render?.SetDepthStencilState(depthStencilState);
        Render?.SetStencilReferenceValue(renderStates.StencilReference);

        Render?.SetFrontFacing(MTLFormats.Metal(renderStates.RasterizerState.FrontFace));
    }
}
