using System.Numerics;
using Hexa.NET.ImGui;

namespace CornellBox.Handlers;

internal class CameraHandler
{
    public CameraHandler(Vector3 position, Vector3 target)
    {
        Position = position;
        Forward = Vector3.Normalize(target - position);
        Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Forward));
    }

    public Vector2 Size { get; private set; } = new(800, 600);

    public Vector3 Position { get; private set; }

    public Vector3 Forward { get; private set; }

    public Vector3 Right { get; private set; }

    public Vector3 Up { get; private set; }

    public float NearPlane { get; set; } = 0.1f;

    public float FarPlane { get; set; } = 200.0f;

    public float Fov { get; set; } = 45.0f;

    public float Speed { get; set; } = 12.0f;

    public float AspectRatio => Size.X / Size.Y;

    public Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Position + Forward, Up);

    public Matrix4x4 Projection => Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(Fov), AspectRatio, NearPlane, FarPlane);

    public void Update(double deltaSeconds, uint width, uint height)
    {
        Size = new(width, height);

        if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            Rotate(ImGui.GetIO().MouseDelta);
        }

        float distance = Speed * (float)deltaSeconds;

        if (ImGui.IsKeyDown(ImGuiKey.W))
        {
            Position += Forward * distance;
        }

        if (ImGui.IsKeyDown(ImGuiKey.S))
        {
            Position -= Forward * distance;
        }

        if (ImGui.IsKeyDown(ImGuiKey.A))
        {
            Position -= Right * distance;
        }

        if (ImGui.IsKeyDown(ImGuiKey.D))
        {
            Position += Right * distance;
        }

        if (ImGui.IsKeyDown(ImGuiKey.Q))
        {
            Position -= Up * distance;
        }

        if (ImGui.IsKeyDown(ImGuiKey.E))
        {
            Position += Up * distance;
        }
    }

    private void Rotate(Vector2 delta)
    {
        const float ClipRadians = 89.0f * MathF.PI / 180.0f;

        float yaw = -(delta.X * MathF.PI / Size.X);
        float pitch = -(delta.Y * MathF.PI / Size.Y);
        float newPitch = Math.Clamp(MathF.Asin(Forward.Y) + pitch, -ClipRadians, ClipRadians);

        pitch = newPitch - MathF.Asin(Forward.Y);

        Forward = Vector3.TransformNormal(Forward, Matrix4x4.CreateFromAxisAngle(Up, yaw));
        Forward = Vector3.TransformNormal(Forward, Matrix4x4.CreateFromAxisAngle(Right, pitch));
        Forward = Vector3.Normalize(Forward);
        Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Forward));
    }
}
