using System.Numerics;
using Silk.NET.Input;

namespace CornellBox.Handlers;

internal class CameraHandler
{
    private readonly HashSet<Key> keyDowns = [];

    private Vector2? lastMousePosition;

    public CameraHandler(Matrix4x4 initial)
    {
        Position = Vector3.Transform(Position, initial);
        Forward = Vector3.TransformNormal(Forward, initial);

        Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Forward));
    }

    public Vector2 Size { get; private set; } = new(800, 600);

    public Vector3 Position { get; private set; } = Vector3.Zero;

    public Vector3 Forward { get; private set; } = Vector3.UnitZ;

    public Vector3 Right { get; private set; } = Vector3.UnitX;

    public Vector3 Up { get; private set; } = Vector3.UnitY;

    public float NearPlane { get; set; } = 0.1f;

    public float FarPlane { get; set; } = 200.0f;

    public float Fov { get; set; } = 45.0f;

    public float Speed { get; set; } = 12.0f;

    public float AspectRatio => Size.X / Size.Y;

    public Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Position + Forward, Up);

    public Matrix4x4 Projection => Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(Fov), AspectRatio, NearPlane, FarPlane);

    public void Update(double deltaSeconds, uint width, uint height, ReadOnlySpan<InputEvent> inputs = default)
    {
        Size = new(width, height);
        ProcessInput(inputs);

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

    private void ProcessInput(ReadOnlySpan<InputEvent> inputs)
    {
        foreach (InputEvent input in inputs)
        {
            switch (input.Type)
            {
                case InputEventType.MouseDown when input.MouseButton is MouseButton.Right:
                    lastMousePosition = input.Position;
                    break;

                case InputEventType.MouseUp when input.MouseButton is MouseButton.Right:
                    lastMousePosition = null;
                    break;

                case InputEventType.MouseMove:
                    OnMouseMove(input.Position);
                    break;

                case InputEventType.KeyDown:
                    keyDowns.Add(input.Key);
                    break;

                case InputEventType.KeyUp:
                    keyDowns.Remove(input.Key);
                    break;

                case InputEventType.Reset:
                    keyDowns.Clear();
                    lastMousePosition = null;
                    break;
            }
        }
    }

    private void OnMouseMove(Vector2 position)
    {
        const float ClipRadians = 89.0f * MathF.PI / 180.0f;

        if (!lastMousePosition.HasValue)
        {
            return;
        }

        float pixelToRadianX = MathF.PI / Size.X;
        float pixelToRadianY = MathF.PI / Size.Y;
        Vector2 delta = position - lastMousePosition.Value;
        float yaw = -(delta.X * pixelToRadianX);
        float pitch = -(delta.Y * pixelToRadianY);
        float newPitch = Math.Clamp(MathF.Asin(Forward.Y) + pitch, -ClipRadians, ClipRadians);

        pitch = newPitch - MathF.Asin(Forward.Y);

        Forward = Vector3.TransformNormal(Forward, Matrix4x4.CreateFromAxisAngle(Up, yaw));
        Forward = Vector3.TransformNormal(Forward, Matrix4x4.CreateFromAxisAngle(Right, pitch));
        Forward = Vector3.Normalize(Forward);
        Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Forward));

        lastMousePosition = position;
    }
}
