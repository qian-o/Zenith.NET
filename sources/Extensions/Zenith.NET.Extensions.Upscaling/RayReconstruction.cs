namespace Zenith.NET.Extensions.Upscaling;

public abstract class RayReconstruction : GraphicsResource
{
    private Key key;

    internal RayReconstruction(GraphicsContext context, RayReconstructionDesc desc) : base(context)
    {
        Desc = desc;
        Output = UpscalingBridge.CreateOutput(context, desc.Format, desc.OutputWidth, desc.OutputHeight);
    }

    public RayReconstructionDesc Desc { get; }

    public Texture Output { get; }

    public void Dispatch(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        commandBuffer.BeginDebugEvent("Ray Reconstruction");

        Key newKey = new(args);

        if (!key.Equals(newKey))
        {
            key = newKey;
            args.Reset = true;

            RebuildImpl(args);
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

    protected abstract void RebuildImpl(RayReconstructionArgs args);

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args);

    protected override void Destroy()
    {
        Output.Dispose();
    }

    private readonly struct Key(RayReconstructionArgs args)
    {
        public readonly PixelFormat Color = args.Color.Desc.Format;

        public readonly PixelFormat Depth = args.Depth.Desc.Format;

        public readonly PixelFormat MotionVectors = args.MotionVectors.Desc.Format;

        public readonly PixelFormat DiffuseAlbedo = args.DiffuseAlbedo.Desc.Format;

        public readonly PixelFormat SpecularAlbedo = args.SpecularAlbedo.Desc.Format;

        public readonly PixelFormat Normals = args.Normals.Desc.Format;

        public readonly PixelFormat Roughness = args.Roughness.Desc.Format;

        public readonly PixelFormat SpecularHitDistance = args.SpecularHitDistance?.Desc.Format ?? default;

        public readonly PixelFormat ReactiveMask = args.ReactiveMask?.Desc.Format ?? default;

        public readonly PixelFormat TransparencyOverlay = args.TransparencyOverlay?.Desc.Format ?? default;

        public readonly PixelFormat Exposure = args.Exposure?.Desc.Format ?? default;

        public readonly bool DepthReversed = UpscalingHelper.IsDepthReversed(args.Projection);

        public readonly bool DepthInfinite = UpscalingHelper.IsDepthInfinite(args.Projection);

        public bool Equals(Key other)
        {
            return Color == other.Color && Depth == other.Depth && MotionVectors == other.MotionVectors && DiffuseAlbedo == other.DiffuseAlbedo && SpecularAlbedo == other.SpecularAlbedo && Normals == other.Normals && Roughness == other.Roughness && SpecularHitDistance == other.SpecularHitDistance && ReactiveMask == other.ReactiveMask && TransparencyOverlay == other.TransparencyOverlay && Exposure == other.Exposure && DepthReversed == other.DepthReversed && DepthInfinite == other.DepthInfinite;
        }
    }
}
