using System.Numerics;
using CornellBox.Models;
using Silk.NET.Input;

namespace CornellBox.Handlers;

internal class CameraHandler
{
    private readonly HashSet<Key> keyDowns = [];

    private Vector2 size = new(800.0f, 600.0f);
    private Vector2? lastMousePosition;

    public CameraHandler(Vector3 position, Vector3 target)
    {
        Position = position;
        Forward = Vector3.Normalize(target - position);
        Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Forward));
    }

    public Vector3 Position { get; private set; }

    public Vector3 Forward { get; private set; }

    public Vector3 Right { get; private set; }

    public Vector3 Up { get; private set; }

    public float NearPlane { get; set; } = 0.1f;

    public float FarPlane { get; set; } = 200.0f;

    public float Fov { get; set; } = 45.0f;

    public float Speed { get; set; } = 12.0f;

    public float AspectRatio => size.X / size.Y;

    public Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Position + Forward, Up);

    public Matrix4x4 Projection => Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(Fov), AspectRatio, NearPlane, FarPlane);

    public void Update(double deltaSeconds, uint width, uint height, ReadOnlySpan<Input> inputs)
    {
        size = new(width, height);

        foreach (Input input in inputs)
        {
            switch (input.Type)
            {
                case InputType.MouseDown when input.Button is MouseButton.Right:
                    lastMousePosition = input.Position;
                    break;

                case InputType.MouseUp when input.Button is MouseButton.Right:
                    lastMousePosition = null;
                    break;

                case InputType.MouseMove:
                    Rotate(input.Position);
                    break;

                case InputType.KeyDown:
                    keyDowns.Add(input.Key);
                    break;

                case InputType.KeyUp:
                    keyDowns.Remove(input.Key);
                    break;
            }
        }

        float distance = Speed * (float)deltaSeconds;

        if (keyDowns.Contains(Key.W))
        {
            Position += Forward * distance;
        }

        if (keyDowns.Contains(Key.S))
        {
            Position -= Forward * distance;
        }

        if (keyDowns.Contains(Key.A))
        {
            Position -= Right * distance;
        }

        if (keyDowns.Contains(Key.D))
        {
            Position += Right * distance;
        }

        if (keyDowns.Contains(Key.Q))
        {
            Position -= Up * distance;
        }

        if (keyDowns.Contains(Key.E))
        {
            Position += Up * distance;
        }
    }

    private void Rotate(Vector2 position)
    {
        const float ClipRadians = 89.0f * MathF.PI / 180.0f;

        if (!lastMousePosition.HasValue)
        {
            return;
        }

        Vector2 delta = position - lastMousePosition.Value;
        float yaw = -(delta.X * (MathF.PI / size.X));
        float pitch = -(delta.Y * (MathF.PI / size.Y));
        float previousPitch = MathF.Asin(Forward.Y);

        pitch = Math.Clamp(previousPitch + pitch, -ClipRadians, ClipRadians) - previousPitch;

        Forward = Vector3.TransformNormal(Forward, Matrix4x4.CreateFromAxisAngle(Up, yaw));
        Forward = Vector3.TransformNormal(Forward, Matrix4x4.CreateFromAxisAngle(Right, pitch));
        Forward = Vector3.Normalize(Forward);
        Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Forward));

        lastMousePosition = position;
    }
}
