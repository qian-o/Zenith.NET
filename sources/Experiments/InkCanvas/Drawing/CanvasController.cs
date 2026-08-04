using System.Numerics;
using Silk.NET.Input;
using Zenith.NET;
using Zenith.NET.Extensions.Skia;

namespace InkCanvas.Drawing;

internal class CanvasController : IDisposable
{
    private readonly Canvas canvas = new();

    private SKTexture texture;

    public CanvasController(GraphicsContext context, IInputContext input, uint width, uint height)
    {
        Context = context;

        texture = CreateTexture(width, height);

        IMouse mouse = input.Mice[0];
        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.MouseMove += OnMouseMove;
    }

    public GraphicsContext Context { get; }

    public void Render(CommandBuffer commandBuffer, Texture target, Vector2 dpiScale)
    {
        float width = texture.Desc.Width / dpiScale.X;
        float height = texture.Desc.Height / dpiScale.Y;

        texture.Render((skiaCanvas) =>
        {
            skiaCanvas.Save();
            skiaCanvas.Scale(dpiScale.X, dpiScale.Y);

            canvas.Draw(skiaCanvas, width, height);

            skiaCanvas.Restore();
        });

        commandBuffer.Transition(texture, default, TextureLayout.ColorAttachment, TextureLayout.CopySrc);
        commandBuffer.CopyTexture(texture, default, default, target, default, default, new()
        {
            Width = texture.Desc.Width,
            Height = texture.Desc.Height,
            Depth = 1
        });
        commandBuffer.Transition(texture, default, TextureLayout.CopySrc, TextureLayout.ColorAttachment);
    }

    public void Resize(uint width, uint height)
    {
        texture.Dispose();
        texture = CreateTexture(width, height);
    }

    public void Dispose()
    {
        canvas.Dispose();
        texture.Dispose();
    }

    private SKTexture CreateTexture(uint width, uint height)
    {
        return Context.CreateSKTexture(new()
        {
            Format = PixelFormat.B8G8R8A8UNorm,
            Width = width,
            Height = height,
            IsMultisamplingEnabled = true
        });
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Left or MouseButton.Right)
        {
            canvas.PointerDown(new(mouse.Position.X, mouse.Position.Y), button is MouseButton.Right);
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button is MouseButton.Left or MouseButton.Right)
        {
            canvas.PointerUp(button is MouseButton.Right);
        }
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        canvas.PointerMove(new(position.X, position.Y));
    }
}
