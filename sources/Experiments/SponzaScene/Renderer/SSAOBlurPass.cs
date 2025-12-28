using System.Numerics;
using Hexa.NET.ImGui;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class SSAOBlurPass : FullscreenPass
{
    private readonly Buffer constantBuffer;

    private ResourceSet? resourceSet;

    private int blurSize = 2;

    public SSAOBlurPass() : base("SSAOBlurPass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(BlurConstants),
            StrideInBytes = (uint)sizeof(BlurConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });
    }

    protected override string ShaderName => "SSAOBlur";

    protected override Output Output => RenderContext.SSAOOutput;

    protected override ResourceLayout? CreateResourceLayout()
    {
        return App.Context.CreateResourceLayout(new()
        {
            Bindings =
            [
                new() { Type = ResourceType.ConstantBuffer, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Sampler, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel }
            ]
        });
    }

    protected override ResourceSet EnsureResourceSet(ResourceLayout resourceLayout, RenderContext context)
    {
        return resourceSet ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                context.SSAO!,
                App.PointSampler
            ]
        });
    }

    protected override (FrameBuffer? FrameBuffer, ClearValue ClearValue) GetTarget(RenderContext context)
    {
        return (context.SSAOBlurFrameBuffer, ClearValues.Default);
    }

    protected override void UpdateResources(RenderContext context)
    {
        constantBuffer.Upload([new BlurConstants
        {
            TexelSize = new Vector2(1.0f / context.Width, 1.0f / context.Height),
            BlurSize = blurSize
        }], 0);
    }

    public override void DebugUI(RenderContext context)
    {
        if (ImGui.Begin("SSAO Blur"))
        {
            ImGui.SliderInt("Blur Size", ref blurSize, 1, 8);

            ImGui.Separator();

            Vector2 size = new((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2.0f);
            size = size with { Y = size.X * context.Height / context.Width };

            ImGui.BeginGroup();
            ImGui.Text("Raw SSAO");
            ImGui.Image(App.Binding(context.SSAO!), size);
            ImGui.EndGroup();

            ImGui.SameLine();

            ImGui.BeginGroup();
            ImGui.Text("Blurred SSAO");
            ImGui.Image(App.Binding(context.SSAOBlurred!), size);
            ImGui.EndGroup();
        }
        ImGui.End();
    }

    public override void Resize(uint width, uint height)
    {
        resourceSet?.Dispose();
        resourceSet = null;
    }

    protected override void Destroy()
    {
        resourceSet?.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    private struct BlurConstants
    {
        public Vector2 TexelSize;

        public int BlurSize;
    }
}