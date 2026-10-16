using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class CompositePass(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight) : Pass(renderWidth, renderHeight, displayWidth, displayHeight)
{
    private Buffer[] buffers = [];
    private ComputePipeline pipeline = null!;

    protected override void Initialize()
    {
        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Composite.slang"), "CSMain"));

        buffers = new Buffer[App.SlotCount];

        for (int i = 0; i < buffers.Length; i++)
        {
            buffers[i] = App.Context.CreateBuffer(new()
            {
                SizeInBytes = (uint)sizeof(Constants),
                Usages = BufferUsages.Constant,
                Residency = MemoryResidency.CpuWriteOnly
            });
        }

        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        Constants constants = new()
        {
            Width = DisplayWidth,
            Height = DisplayHeight,
            Color = args.Color.SampledHandle,
            GeneratedColor = args.GeneratedColor?.SampledHandle ?? default,
            UI = args.UI.SampledHandle,
            Output = args.BackBuffer.StorageHandle,
            GeneratedOutput = args.GeneratedColor is not null ? args.GeneratedFrame.StorageHandle : default
        };

        Buffer buffer = buffers[args.Slot];

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });

        commandBuffer.Transition(args.BackBuffer, default, TextureLayout.Undefined, TextureLayout.Storage);

        if (args.GeneratedColor is not null)
        {
            commandBuffer.Transition(args.GeneratedFrame, default, TextureLayout.Undefined, TextureLayout.Storage);
        }

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((DisplayWidth + 7) / 8, (DisplayHeight + 7) / 8, args.GeneratedColor is not null ? 2u : 1u);
        commandBuffer.Transition(args.BackBuffer, default, TextureLayout.Storage, TextureLayout.CopySrc);

        if (args.GeneratedColor is not null)
        {
            commandBuffer.Transition(args.GeneratedFrame, default, TextureLayout.Storage, TextureLayout.CopySrc);
        }
    }

    protected override void ResizeImpl()
    {
    }

    protected override void Destroy()
    {
        pipeline.Dispose();

        foreach (Buffer buffer in buffers)
        {
            buffer.Dispose();
        }
    }
}

[StructLayout(LayoutKind.Explicit, Size = 64)]
file struct Constants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(16)]
    public ResourceHandle Color;

    [FieldOffset(24)]
    public ResourceHandle GeneratedColor;

    [FieldOffset(32)]
    public ResourceHandle UI;

    [FieldOffset(40)]
    public ResourceHandle Output;

    [FieldOffset(48)]
    public ResourceHandle GeneratedOutput;
}
