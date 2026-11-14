using System.Numerics;

namespace Zenith.NET.Extensions.Upscaling;

public static class UpscalingHelper
{
    public static uint JitterPhaseCount(uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight)
    {
        if (renderWidth is 0 || renderHeight is 0 || (outputWidth <= renderWidth && outputHeight <= renderHeight))
        {
            return 8;
        }

        float scaleX = MathF.Max((float)outputWidth / renderWidth, 1.0f);
        float scaleY = MathF.Max((float)outputHeight / renderHeight, 1.0f);

        return Math.Max((uint)MathF.Ceiling(8.0f * scaleX * scaleY), 8u);
    }

    public static uint JitterPhaseCount(SuperResolutionDesc desc)
    {
        return JitterPhaseCount(desc.RenderWidth, desc.RenderHeight, desc.OutputWidth, desc.OutputHeight);
    }

    public static uint JitterPhaseCount(RayReconstructionDesc desc)
    {
        return JitterPhaseCount(desc.RenderWidth, desc.RenderHeight, desc.OutputWidth, desc.OutputHeight);
    }

    public static Vector2 JitterOffset(uint frameIndex, uint phaseCount)
    {
        if (phaseCount is 0)
        {
            return Vector2.Zero;
        }

        uint sampleIndex = (frameIndex % phaseCount) + 1u;

        return new(Halton(sampleIndex, 2u) - 0.5f, Halton(sampleIndex, 3u) - 0.5f);
    }

    public static Vector2 JitterOffset(uint frameIndex, uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight)
    {
        return JitterOffset(frameIndex, JitterPhaseCount(renderWidth, renderHeight, outputWidth, outputHeight));
    }

    public static Vector2 JitterOffset(uint frameIndex, SuperResolutionDesc desc)
    {
        return JitterOffset(frameIndex, JitterPhaseCount(desc));
    }

    public static Vector2 JitterOffset(uint frameIndex, RayReconstructionDesc desc)
    {
        return JitterOffset(frameIndex, JitterPhaseCount(desc));
    }

    public static Matrix4x4 JitterProjection(Matrix4x4 projection, Vector2 jitterOffset, uint renderWidth, uint renderHeight)
    {
        if (renderWidth is 0 || renderHeight is 0)
        {
            return projection;
        }

        return projection * Matrix4x4.CreateTranslation(-2.0f * jitterOffset.X / renderWidth, 2.0f * jitterOffset.Y / renderHeight, 0.0f);
    }

    public static Matrix4x4 JitterProjection(Matrix4x4 projection, Vector2 jitterOffset, SuperResolutionDesc desc)
    {
        return JitterProjection(projection, jitterOffset, desc.RenderWidth, desc.RenderHeight);
    }

    public static Matrix4x4 JitterProjection(Matrix4x4 projection, Vector2 jitterOffset, RayReconstructionDesc desc)
    {
        return JitterProjection(projection, jitterOffset, desc.RenderWidth, desc.RenderHeight);
    }

    private static float Halton(uint index, uint radix)
    {
        float result = 0.0f;
        float fraction = 1.0f;

        while (index > 0u)
        {
            fraction /= radix;
            result += fraction * (index % radix);
            index /= radix;
        }

        return result;
    }
}
