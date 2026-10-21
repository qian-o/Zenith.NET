using System.Numerics;
using Silk.NET.Input;
using Zenith.NET;

namespace CornellBox.Handlers;

internal class CameraHandler : DisposableObject
{
    private readonly IMouse mouse;
    private readonly IKeyboard keyboard;
    private readonly HashSet<Key> keyDowns = [];

    private Vector2? lastMousePosition;

    public CameraHandler(IInputContext input, Vector3 position, Vector3 target)
    {
        mouse = input.Mice[0];
        keyboard = input.Keyboards[0];

        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.MouseMove += OnMouseMove;
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

    public void Update(double deltaSeconds, uint width, uint height)
    {
        Size = new(width, height);

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

    protected override void Destroy()
    {
        if (keyboard is not null)
        {
            keyboard.KeyUp -= OnKeyUp;
            keyboard.KeyDown -= OnKeyDown;
        }

        if (mouse is not null)
        {
            mouse.MouseMove -= OnMouseMove;
            mouse.MouseUp -= OnMouseUp;
            mouse.MouseDown -= OnMouseDown;
        }

        keyDowns.Clear();
        lastMousePosition = null;
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Right)
        {
            lastMousePosition = mouse.Position;
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

    private void OnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        keyDowns.Add(key);
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        keyDowns.Remove(key);
    }
}
