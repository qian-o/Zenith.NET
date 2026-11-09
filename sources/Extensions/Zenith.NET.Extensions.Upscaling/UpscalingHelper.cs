using System.Numerics;

namespace Zenith.NET.Extensions.Upscaling;

internal static class UpscalingHelper
{
    public static float Scale(UpscalingMode mode)
    {
        return mode switch
        {
            UpscalingMode.Native => 1.0f,
            UpscalingMode.Quality => 1.5f,
            UpscalingMode.Balanced => 1.7f,
            UpscalingMode.Performance => 2.0f,
            UpscalingMode.UltraPerformance => 3.0f,
            _ => default
        };
    }

    public static (float NearPlane, float FarPlane, float VerticalFieldOfViewInRadians, float AspectRatio) DecomposePerspective(Matrix4x4 viewToClip)
    {
        float zeroDepthDistance = MathF.Abs(viewToClip.M43 / -viewToClip.M33);
        float unitDepthDistance = MathF.Abs(viewToClip.M43 / (viewToClip.M34 - viewToClip.M33));

        return (MathF.Min(zeroDepthDistance, unitDepthDistance), MathF.Max(zeroDepthDistance, unitDepthDistance), 2.0f * MathF.Atan(1.0f / viewToClip.M22), viewToClip.M22 / viewToClip.M11);
    }
}
