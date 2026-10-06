using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class CompositePass(uint width, uint height) : Pass(width, height)
{
    private Buffer buffer = null!;
    private ComputePipeline pipeline = null!;

    protected override void Initialize()
    {
        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Composite.slang"), "CSMain"));

        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        Constants constants = new()
        {
            Width = Width,
            Height = Height,
            Hudless = args.Color.SampledHandle,
            UI = args.UI.SampledHandle,
            Output = args.BackBuffer.StorageHandle
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });

        commandBuffer.Transition(args.BackBuffer, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((Width + 7) / 8, (Height + 7) / 8, 1);
        commandBuffer.Transition(args.BackBuffer, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    protected override void ResizeImpl()
    {
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        buffer.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 48)]
file struct Constants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(16)]
    public ResourceHandle Hudless;

    [FieldOffset(24)]
    public ResourceHandle UI;

    [FieldOffset(32)]
    public ResourceHandle Output;
}
