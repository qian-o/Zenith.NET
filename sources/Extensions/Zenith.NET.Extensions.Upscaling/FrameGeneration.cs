namespace Zenith.NET.Extensions.Upscaling;

public abstract class FrameGeneration : GraphicsResource
{
    private Key key;

    internal FrameGeneration(GraphicsContext context, FrameGenerationDesc desc) : base(context)
    {
        Desc = desc;
        Output = UpscalingBridge.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);
    }

    public FrameGenerationDesc Desc { get; }

    public Texture Output { get; }

    public void Dispatch(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        commandBuffer.BeginDebugEvent("Frame Generation");

        Key newKey = new(args);

        if (!key.Equals(newKey))
        {
            key = newKey;
            args.Reset = true;

            Build(args);
        }

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
        Output.Name = name;
    }

    protected abstract void Build(FrameGenerationArgs args);

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args);

    protected override void Destroy()
    {
        Output.Dispose();
    }

    private readonly struct Key(FrameGenerationArgs args)
    {
        public readonly PixelFormat Color = args.Color.Desc.Format;

        public readonly PixelFormat Depth = args.Depth.Desc.Format;

        public readonly PixelFormat MotionVectors = args.MotionVectors.Desc.Format;

        public readonly PixelFormat Hudless = args.Hudless?.Desc.Format ?? default;

        public readonly bool DepthReversed = UpscalingHelper.IsDepthReversed(args.Projection);

        public readonly bool DepthInfinite = UpscalingHelper.IsDepthInfinite(args.Projection);

        public bool Equals(Key other)
        {
            return Color == other.Color && Depth == other.Depth && MotionVectors == other.MotionVectors && Hudless == other.Hudless && DepthReversed == other.DepthReversed && DepthInfinite == other.DepthInfinite;
        }
    }
}
