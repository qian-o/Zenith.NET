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

        return Math.Max((uint)MathF.Ceiling(8.0f * scaleX * scaleY), 8);
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

        uint sampleIndex = (frameIndex % phaseCount) + 1;

        return new(Halton(sampleIndex, 2) - 0.5f, Halton(sampleIndex, 3) - 0.5f);
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

    internal static (uint Width, uint Height) RenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        float scale = quality switch
        {
            UpscalingQuality.Native => 1.0f,
            UpscalingQuality.Quality => 1.5f,
            UpscalingQuality.Balanced => 1.7f,
            UpscalingQuality.Performance => 2.0f,
            UpscalingQuality.UltraPerformance => 3.0f,
            _ => 1.0f
        };

        return (ScaleDimension(outputWidth, scale), ScaleDimension(outputHeight, scale));
    }

    internal static bool IsDepthReversed(Matrix4x4 projection)
    {
        DecomposePerspective(projection, out _, out _, out _, out _, out bool depthReversed, out _);

        return depthReversed;
    }

    internal static void DecomposePerspective(Matrix4x4 projection, out float nearPlane, out float farPlane, out float verticalFieldOfViewInRadians, out float aspectRatio, out bool depthReversed, out bool depthInfinite)
    {
        const float Epsilon = 1e-6f;

        float zeroDenominator = -projection.M33;
        float unitDenominator = projection.M34 - projection.M33;

        float zeroDepthDistance = MathF.Abs(zeroDenominator) <= Epsilon ? float.PositiveInfinity : MathF.Abs(projection.M43 / zeroDenominator);
        float unitDepthDistance = MathF.Abs(unitDenominator) <= Epsilon ? float.PositiveInfinity : MathF.Abs(projection.M43 / unitDenominator);

        depthReversed = zeroDepthDistance > unitDepthDistance;
        nearPlane = MathF.Min(zeroDepthDistance, unitDepthDistance);
        farPlane = MathF.Max(zeroDepthDistance, unitDepthDistance);
        depthInfinite = float.IsInfinity(farPlane) || float.IsNaN(farPlane);

        if (depthInfinite)
        {
            farPlane = float.PositiveInfinity;
        }

        verticalFieldOfViewInRadians = 2.0f * MathF.Atan(1.0f / MathF.Abs(projection.M22));
        aspectRatio = MathF.Abs(projection.M22 / projection.M11);
    }

    private static uint ScaleDimension(uint size, float scale)
    {
        return size is 0 ? 0 : Math.Max((uint)MathF.Floor((size / scale) + 0.5f), 1);
    }

    private static float Halton(uint index, uint radix)
    {
        float result = 0.0f;
        float fraction = 1.0f;

        while (index > 0)
        {
            fraction /= radix;
            result += fraction * (index % radix);
            index /= radix;
        }

        return result;
    }
}
