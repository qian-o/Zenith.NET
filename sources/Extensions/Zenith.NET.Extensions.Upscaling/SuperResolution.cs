namespace Zenith.NET.Extensions.Upscaling;

public abstract class SuperResolution : GraphicsResource
{
    private Key key;

    internal SuperResolution(GraphicsContext context, SuperResolutionDesc desc) : base(context)
    {
        Desc = desc;
        Output = UpscalingBridge.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);
    }

    public SuperResolutionDesc Desc { get; }

    public Texture Output { get; }

    public void Dispatch(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        commandBuffer.BeginDebugEvent("Super Resolution");

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

    protected abstract void Build(SuperResolutionArgs args);

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args);

    protected override void Destroy()
    {
        Output.Dispose();
    }

    private readonly struct Key(SuperResolutionArgs args)
    {
        public readonly PixelFormat Color = args.Color.Desc.Format;

        public readonly PixelFormat Depth = args.Depth.Desc.Format;

        public readonly PixelFormat MotionVectors = args.MotionVectors.Desc.Format;

        public readonly PixelFormat ReactiveMask = args.ReactiveMask?.Desc.Format ?? default;

        public readonly PixelFormat Exposure = args.Exposure?.Desc.Format ?? default;

        public readonly bool DepthReversed = UpscalingHelper.IsDepthReversed(args.Projection);

        public readonly bool DepthInfinite = UpscalingHelper.IsDepthInfinite(args.Projection);

        public bool Equals(Key other)
        {
            return Color == other.Color
                   && Depth == other.Depth
                   && MotionVectors == other.MotionVectors
                   && ReactiveMask == other.ReactiveMask
                   && Exposure == other.Exposure
                   && DepthReversed == other.DepthReversed
                   && DepthInfinite == other.DepthInfinite;
        }
    }
}
