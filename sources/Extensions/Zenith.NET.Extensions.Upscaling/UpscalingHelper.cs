using System.Numerics;

namespace Zenith.NET.Extensions.Upscaling;

internal static class UpscalingHelper
{
    public static (float NearPlane, float FarPlane, float VerticalFieldOfViewInRadians, float AspectRatio) DecomposePerspective(Matrix4x4 viewToClip)
    {
        float zeroDepthDistance = MathF.Abs(viewToClip.M43 / -viewToClip.M33);
        float unitDepthDistance = MathF.Abs(viewToClip.M43 / (viewToClip.M34 - viewToClip.M33));

        return (MathF.Min(zeroDepthDistance, unitDepthDistance), MathF.Max(zeroDepthDistance, unitDepthDistance), 2.0f * MathF.Atan(1.0f / viewToClip.M22), viewToClip.M22 / viewToClip.M11);
    }
}
