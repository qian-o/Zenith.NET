using System.IO;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Zenith.NET.Views;
using Zenith.NET.Views.WPF;
using Buffer = Zenith.NET.Buffer;

namespace Sample.WPF.ViewModels;

public unsafe partial class MainViewModel : ObservableRecipient
{
    private struct Constants
    {
        public Vector2 Resolution;

        public float TotalTime;
    }

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer constantsBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly ResourceSet resourceSet;
    private readonly GraphicsPipeline graphicsPipeline;

    public MainViewModel()
    {
        float[] vertices =
        [
            -1f, -1f, 0f, 0f,
             1f, -1f, 1f, 0f,
             1f,  1f, 1f, 1f,
            -1f,  1f, 0f, 1f,
        ];

        uint[] indices =
        [
            0, 1, 2,
            2, 3, 0,
        ];

        vertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(float) * vertices.Length),
            StrideInBytes = sizeof(float) * 4,
            Flags = BufferUsageFlags.Vertex
        });
        vertexBuffer.Upload(vertices, 0);

        indexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Length),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.Index
        });
        indexBuffer.Upload(indices, 0);

        constantsBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            StrideInBytes = (uint)sizeof(Constants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.Dynamic
        });

        resourceLayout = App.Context.CreateResourceLayout(new() { Bindings = [new() { Type = ResourceType.ConstantBuffer, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel }] });
        resourceSet = App.Context.CreateResourceSet(new() { Layout = resourceLayout, Resources = [constantsBuffer] });

        using Shader vs = App.Context.LoadShaderFromFile(Path.Combine(AppContext.BaseDirectory, "Shaders", "Vortex.slang"), "VSMain", ShaderStageFlags.Vertex);
        using Shader ps = App.Context.LoadShaderFromFile(Path.Combine(AppContext.BaseDirectory, "Shaders", "Vortex.slang"), "PSMain", ShaderStageFlags.Pixel);

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float2, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float2, Semantic = ElementSemantic.TexCoord });

        graphicsPipeline = App.Context.CreateGraphicsPipeline(new()
        {
            RenderStates = new()
            {
                RasterizerState = RasterizerStates.Default,
                DepthStencilState = DepthStencilStates.Default,
                BlendState = BlendStates.Default
            },
            Vertex = vs,
            Pixel = ps,
            ResourceLayouts = [resourceLayout],
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = ZenithView.Output
        });
    }

    [ObservableProperty]
    private double actualWidth;

    [ObservableProperty]
    private double actualHeight;

    [RelayCommand]
    private void Update(UpdateEventArgs args)
    {
        constantsBuffer.Upload([new Constants() { Resolution = new((float)ActualWidth, (float)ActualHeight), TotalTime = (float)args.TotalTime }], 0);
    }

    [RelayCommand]
    private void Render(RenderEventArgs args)
    {
        CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        commandBuffer.BindFrameBuffer(args.FrameBuffer, ClearValues.Default);

        commandBuffer.BindPipeline(graphicsPipeline);
        commandBuffer.BindVertexBuffer(vertexBuffer, 0, 0);
        commandBuffer.BindIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
        commandBuffer.BindResourceSet(resourceSet, 0);
        commandBuffer.DrawIndexed(6, 1, 0, 0, 0);

        commandBuffer.Submit();

        App.Context.Graphics.WaitIdle();
    }
}
