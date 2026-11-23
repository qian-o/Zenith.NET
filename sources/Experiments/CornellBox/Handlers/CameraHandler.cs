using System.Numerics;
using Silk.NET.Input;
using Zenith.NET;

namespace CornellBox.Handlers;

internal class CameraHandler : DisposableObject
{
    private const float MaxPitchInRadians = (89.0f * MathF.PI) / 180.0f;

    private readonly IMouse mouse;
    private readonly IKeyboard keyboard;
    private readonly HashSet<Key> keyDowns = [];
    private readonly Vector3 initialPosition;
    private readonly Vector3 initialTarget;

    private Vector2? lastMousePosition;
    private float yawInRadians;
    private float pitchInRadians;

    public CameraHandler(IInputContext input, Vector3 position, Vector3 target)
    {
        mouse = input.Mice[0];
        keyboard = input.Keyboards[0];

        initialPosition = position;
        initialTarget = target;

        SetLookAt(position, target);

        mouse.MouseMove += OnMouseMove;
        keyboard.KeyDown += OnKeyDown;
        keyboard.KeyUp += OnKeyUp;
    }

    public Vector3 Position { get; private set; }

    public Vector3 Forward { get; private set; }

    public Vector3 Right { get; private set; }

    public Vector3 Up { get; private set; }

    public float NearPlane { get; set; } = 0.1f;

    public float FarPlane { get; set; } = 200.0f;

    public float Fov { get; set; } = 45.0f;

    public float Speed { get; set; } = 12.0f;

    public Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Position + Forward, Up);

    public Matrix4x4 Projection => Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(Fov), (float)App.LogicalWidth / App.LogicalHeight, NearPlane, FarPlane);

    public void Reset()
    {
        lastMousePosition = null;
        keyDowns.Clear();
        SetLookAt(initialPosition, initialTarget);
    }

    public void Update(double deltaSeconds)
    {
        float multiplier = (keyDowns.Contains(Key.ShiftLeft) || keyDowns.Contains(Key.ShiftRight)) ? 2.5f : 1.0f;
        float distance = Speed * multiplier * (float)deltaSeconds;

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
        }

        keyDowns.Clear();
        lastMousePosition = null;
    }

    private void SetLookAt(Vector3 position, Vector3 target)
    {
        Vector3 direction = Vector3.Normalize(target - position);

        Position = position;
        pitchInRadians = Math.Clamp(MathF.Asin(Math.Clamp(direction.Y, -1.0f, 1.0f)), -MaxPitchInRadians, MaxPitchInRadians);
        yawInRadians = MathF.Atan2(direction.X, direction.Z);

        UpdateBasis();
    }

    private void UpdateBasis()
    {
        float cosPitch = MathF.Cos(pitchInRadians);

        Forward = Vector3.Normalize(new(MathF.Sin(yawInRadians) * cosPitch, MathF.Sin(pitchInRadians), MathF.Cos(yawInRadians) * cosPitch));
        Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Forward));
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (mouse.IsButtonPressed(MouseButton.Right) && lastMousePosition is Vector2 lastPosition)
        {
            Vector2 delta = position - lastPosition;

            yawInRadians -= delta.X * (MathF.PI / App.LogicalWidth);
            pitchInRadians = Math.Clamp(pitchInRadians - (delta.Y * (MathF.PI / App.LogicalHeight)), -MaxPitchInRadians, MaxPitchInRadians);
            UpdateBasis();
        }

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
