using System.Numerics;
using Silk.NET.Input;

namespace SponzaScene.Models;

internal class CameraController
{
    private readonly HashSet<Key> keyDowns = [];

    private Vector2? lastMousePosition;

    public CameraController(IInputContext inputContext, Matrix4x4 initial)
    {
        IMouse mouse = inputContext.Mice[0];
        mouse.MouseDown += Mouse_MouseDown;
        mouse.MouseUp += Mouse_MouseUp;
        mouse.MouseMove += Mouse_MouseMove;

        IKeyboard keyboard = inputContext.Keyboards[0];
        keyboard.KeyDown += OnKeyDown;
        keyboard.KeyUp += OnKeyUp;

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

    public float FarPlane { get; set; } = 1000.0f;

    public float Fov { get; set; } = 40.0f;

    public float Speed { get; set; } = 2.5f;

    public float AspectRatio => Size.X / Size.Y;

    public Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Position + Forward, Up);

    public Matrix4x4 Projection => Matrix4x4.CreatePerspectiveFieldOfView(Fov * MathF.PI / 180.0f, AspectRatio, NearPlane, FarPlane);

    public void Update(double delta, uint width, uint height)
    {
        Size = new(width, height);

        if (keyDowns.Contains(Key.W))
        {
            Position += Forward * Speed * (float)delta;
        }

        if (keyDowns.Contains(Key.S))
        {
            Position -= Forward * Speed * (float)delta;
        }

        if (keyDowns.Contains(Key.A))
        {
            Position -= Right * Speed * (float)delta;
        }

        if (keyDowns.Contains(Key.D))
        {
            Position += Right * Speed * (float)delta;
        }

        if (keyDowns.Contains(Key.Q))
        {
            Position -= Up * Speed * (float)delta;
        }

        if (keyDowns.Contains(Key.E))
        {
            Position += Up * Speed * (float)delta;
        }
    }

    private void Mouse_MouseDown(IMouse arg1, MouseButton arg2)
    {
        if (arg2 is MouseButton.Right)
        {
            lastMousePosition = arg1.Position;
        }
    }

    private void Mouse_MouseUp(IMouse arg1, MouseButton arg2)
    {
        if (arg2 is MouseButton.Right)
        {
            lastMousePosition = null;
        }
    }

    private void Mouse_MouseMove(IMouse mouse, Vector2 vector)
    {
        const float clipRadians = 89.0f * MathF.PI / 180.0f;

        if (lastMousePosition.HasValue)
        {
            float pixelToRadianX = MathF.PI / Size.X;
            float pixelToRadianY = MathF.PI / Size.Y;

            Vector2 delta = mouse.Position - lastMousePosition.Value;

            float yaw = -(delta.X * pixelToRadianX);
            float pitch = -(delta.Y * pixelToRadianY);

            float newPitch = MathF.Asin(Forward.Y) + pitch;

            if (newPitch > clipRadians)
            {
                newPitch = clipRadians;
            }
            else if (newPitch < -clipRadians)
            {
                newPitch = -clipRadians;
            }

            pitch = newPitch - MathF.Asin(Forward.Y);

            Forward = Vector3.TransformNormal(Forward, Matrix4x4.CreateFromAxisAngle(Up, yaw));
            Forward = Vector3.TransformNormal(Forward, Matrix4x4.CreateFromAxisAngle(Right, pitch));

            Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
            Up = Vector3.Normalize(Vector3.Cross(Right, Forward));

            lastMousePosition = mouse.Position;
        }
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        keyDowns.Add(key);
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int arg3)
    {
        keyDowns.Remove(key);
    }
}
