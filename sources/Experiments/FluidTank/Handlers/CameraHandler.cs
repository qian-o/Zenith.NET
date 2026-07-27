using System.Numerics;
using Silk.NET.Input;

namespace FluidTank.Handlers;

internal class CameraHandler
{
    private readonly HashSet<Key> keyDowns = [];

    private Vector2? lastMousePosition;
    private Vector2? clickPosition;

    public CameraHandler(IInputContext input, Vector3 position, Vector3 target)
    {
        IMouse mouse = input.Mice[0];
        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.MouseMove += OnMouseMove;

        IKeyboard keyboard = input.Keyboards[0];
        keyboard.KeyDown += OnKeyDown;
        keyboard.KeyUp += OnKeyUp;

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

    public void Update(double delta, uint width, uint height, bool allowMovement)
    {
        Size = new(width, height);

        if (!allowMovement)
        {
            return;
        }

        float distance = Speed * (float)delta;

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

    public bool TryConsumeClickRay(out Vector3 origin, out Vector3 direction)
    {
        if (!clickPosition.HasValue)
        {
            origin = default;
            direction = default;

            return false;
        }

        Vector2 position = clickPosition.Value;
        clickPosition = null;

        float x = (position.X / Size.X) * 2.0f - 1.0f;
        float y = 1.0f - (position.Y / Size.Y) * 2.0f;

        Matrix4x4.Invert(Projection, out Matrix4x4 invProjection);
        Matrix4x4.Invert(View, out Matrix4x4 invView);

        Vector4 target = Vector4.Transform(new Vector4(x, y, 1.0f, 1.0f), invProjection);
        Vector3 localDirection = Vector3.Normalize(new(target.X / target.W, target.Y / target.W, target.Z / target.W));

        origin = Position;
        direction = Vector3.Normalize(Vector3.TransformNormal(localDirection, invView));

        return true;
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Right)
        {
            lastMousePosition = mouse.Position;
        }
        else if (button is MouseButton.Left)
        {
            clickPosition = mouse.Position;
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Right)
        {
            lastMousePosition = null;
        }
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
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

    private void OnKeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        keyDowns.Add(key);
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int arg3)
    {
        keyDowns.Remove(key);
    }
}