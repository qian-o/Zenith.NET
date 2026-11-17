using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSFrameGeneration(DLSSVendor vendor, FrameGenerationDesc desc) : FrameGeneration(vendor.Context, desc)
{
    public readonly NGXParameter Parameters = vendor.AllocateParameters();

    public NGXHandle Handle;

    public Matrix4x4 PreviousView;

    public Matrix4x4 PreviousProjection;

    private Key key;

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        commandBuffer.Transition(Output, default, key.Color is PixelFormat.Unknown ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Storage);

        commandBuffer.BeginExternalCommands();

        if (Ensure(commandBuffer, args))
        {
            args.Reset = true;
        }

        vendor.Evaluate(commandBuffer, this, args);

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(Output, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        vendor.ReleaseFeature(Handle);
        vendor.DestroyParameters(Parameters);

        base.Destroy();
    }

    private bool Ensure(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        Key newKey = new(args);

        if (key.Equals(newKey))
        {
            return false;
        }

        key = newKey;

        vendor.Create(commandBuffer, this);

        return true;
    }

    private readonly struct Key(FrameGenerationArgs args)
    {
        public readonly PixelFormat Color = args.Color.Desc.Format;

        public readonly PixelFormat Depth = args.Depth.Desc.Format;

        public readonly PixelFormat MotionVectors = args.MotionVectors.Desc.Format;

        public readonly PixelFormat Hudless = args.Hudless?.Desc.Format ?? default;

        public bool Equals(Key other)
        {
            return Color == other.Color && Depth == other.Depth && MotionVectors == other.MotionVectors && Hudless == other.Hudless;
        }
    }
}
