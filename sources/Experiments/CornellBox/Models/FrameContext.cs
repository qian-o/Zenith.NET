using System.Numerics;
using CornellBox.Handlers;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox.Models;

internal readonly struct FrameContext
{
    public Matrix4x4 View { get; init; }

    public Matrix4x4 Projection { get; init; }

    public Matrix4x4 ViewProjection { get; init; }

    public Matrix4x4 PreviousViewProjection { get; init; }

    public Matrix4x4 JitteredViewProjection { get; init; }

    public Matrix4x4 InverseView { get; init; }

    public Matrix4x4 InverseProjection { get; init; }

    public Matrix4x4 InverseJitteredProjection { get; init; }

    public Matrix4x4 InverseJitteredViewProjection { get; init; }

    public SceneEnvironment Environment { get; init; }

    public Vector3 CameraPosition { get; init; }

    public Vector2 JitterOffset { get; init; }

    public float DeltaTimeInSeconds { get; init; }

    public uint FrameIndex { get; init; }

    public bool Reset { get; init; }

    public bool ResetLighting { get; init; }

    public static FrameContext Create(CameraHandler camera,
                                      SceneEnvironment environment,
                                      Matrix4x4 previousViewProjection,
                                      uint frameIndex,
                                      uint renderWidth,
                                      uint renderHeight,
                                      uint outputWidth,
                                      uint outputHeight,
                                      float deltaTimeInSeconds,
                                      bool isUpscalingEnabled,
                                      bool reset,
                                      bool resetLighting)
    {
        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        Matrix4x4 viewProjection = view * projection;
        Matrix4x4 effectivePreviousViewProjection = reset ? viewProjection : previousViewProjection;
        Vector2 jitterOffset = isUpscalingEnabled ? UpscalingHelper.JitterOffset(frameIndex, renderWidth, renderHeight, outputWidth, outputHeight) : Vector2.Zero;

        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);

        Matrix4x4 jitteredViewProjection;
        Matrix4x4 inverseJitteredProjection;

        if (isUpscalingEnabled)
        {
            Matrix4x4 jitteredProjection = UpscalingHelper.JitterProjection(projection, jitterOffset, renderWidth, renderHeight);
            jitteredViewProjection = view * jitteredProjection;
            Matrix4x4.Invert(jitteredProjection, out inverseJitteredProjection);
        }
        else
        {
            jitteredViewProjection = viewProjection;
            inverseJitteredProjection = inverseProjection;
        }

        Matrix4x4.Invert(jitteredViewProjection, out Matrix4x4 inverseJitteredViewProjection);

        return new()
        {
            View = view,
            Projection = projection,
            ViewProjection = viewProjection,
            PreviousViewProjection = effectivePreviousViewProjection,
            JitteredViewProjection = jitteredViewProjection,
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            InverseJitteredProjection = inverseJitteredProjection,
            InverseJitteredViewProjection = inverseJitteredViewProjection,
            Environment = environment,
            CameraPosition = camera.Position,
            JitterOffset = jitterOffset,
            DeltaTimeInSeconds = deltaTimeInSeconds,
            FrameIndex = frameIndex,
            Reset = reset,
            ResetLighting = resetLighting
        };
    }
}
