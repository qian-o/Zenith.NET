using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using CornellBox.Passes;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox;

internal class Renderer : DisposableObject
{
    private readonly SceneResources scene;
    private readonly PathTracingPass pathTracingPass;
    private readonly TonemapPass tonemapPass;
    private readonly CompositePass compositePass;

    private DLSSRayReconstruction? rayReconstruction;
    private DLSSFrameGeneration? frameGeneration;
    private Texture? reconstructedColor;
    private Texture[] generatedColors = [];
    private Matrix4x4 previousViewProjection;
    private uint width;
    private uint height;
    private uint frameIndex;
    private bool hasHistory;

    public Renderer()
    {
        if ((OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) && App.Context.GraphicsApi is GraphicsApi.DirectX12 or GraphicsApi.Vulkan)
        {
            string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zenith.NET", "CornellBox", "NGX");
            Directory.CreateDirectory(dataPath);

            App.Context.InitializeDLSS(dataPath);

            DLSSCapabilities capabilities = App.Context.DLSSCapabilities;
            RayReconstructionSupported = capabilities.RayReconstructionSupported;
            FrameGenerationSupported = capabilities.FrameGenerationSupported;
            MaxGeneratedFrameCount = FrameGenerationSupported ? Math.Max(capabilities.MaxGeneratedFrameCount, 1) : 0;
        }

        width = App.Width;
        height = App.Height;
        scene = new();
        pathTracingPass = new(width, height);
        tonemapPass = new(width, height);
        compositePass = new(width, height, MaxGeneratedFrameCount);
    }

    public bool RayReconstructionSupported { get; }

    public bool FrameGenerationSupported { get; }

    public uint MaxGeneratedFrameCount { get; }

    public bool IsPaused { get; set; }

    public DLSSMode? RayReconstruction { get; private set; }

    public uint GeneratedFrameCount { get; private set; }

    public Texture UI => compositePass.UI;

    public Texture Color => compositePass.Color;

    public Texture[] GeneratedColors => compositePass.GeneratedColors;

    public Buffer? DisableInterpolation { get; private set; }

    public void Render(CommandBuffer commandBuffer, CameraHandler camera, double deltaSeconds)
    {
        scene.Update(commandBuffer, IsPaused ? 0.0 : deltaSeconds);

        Matrix4x4 viewProjection = camera.View * camera.Projection;

        if (!hasHistory)
        {
            previousViewProjection = viewProjection;
        }

        Vector2 jitter = RayReconstruction.HasValue ? new(Halton(frameIndex + 1, 2) - 0.5f, Halton(frameIndex + 1, 3) - 0.5f) : Vector2.Zero;
        pathTracingPass.Record(commandBuffer, scene, camera, previousViewProjection, jitter, frameIndex, RayReconstruction.HasValue);

        Texture color = pathTracingPass.Color;

        if (rayReconstruction is not null)
        {
            Reconstruct(commandBuffer, camera, jitter);
            color = reconstructedColor!;
        }

        tonemapPass.Record(commandBuffer, color, frameIndex);

        if (frameGeneration is not null)
        {
            Generate(commandBuffer, camera, jitter);
        }

        compositePass.Record(commandBuffer, tonemapPass.Color, generatedColors);

        previousViewProjection = viewProjection;
        hasHistory = true;
        frameIndex++;
    }

    public void SetRayReconstruction(DLSSMode? mode)
    {
        mode = RayReconstructionSupported ? mode : null;

        if (RayReconstruction == mode)
        {
            return;
        }

        RayReconstruction = mode;
        RecreateRenderResources();
    }

    public void SetGeneratedFrameCount(uint generatedFrameCount)
    {
        generatedFrameCount = Math.Min(generatedFrameCount, MaxGeneratedFrameCount);

        if (GeneratedFrameCount == generatedFrameCount)
        {
            return;
        }

        DestroyFrameGeneration();
        compositePass.SetGeneratedFrameCount(generatedFrameCount);
        GeneratedFrameCount = generatedFrameCount;

        if (generatedFrameCount is not 0)
        {
            CreateFrameGeneration();
        }

        ResetHistory();
    }

    public void ResetHistory()
    {
        hasHistory = false;
    }

    public void Resize(uint width, uint height)
    {
        if (width is 0 || height is 0)
        {
            ResetHistory();

            return;
        }

        if (this.width == width && this.height == height)
        {
            return;
        }

        this.width = width;
        this.height = height;

        tonemapPass.Resize(width, height);
        compositePass.Resize(width, height);
        RecreateRenderResources();
    }

    protected override void Destroy()
    {
        DestroyFrameGeneration();
        DestroyRayReconstruction();
        compositePass?.Dispose();
        tonemapPass?.Dispose();
        pathTracingPass?.Dispose();
        scene?.Dispose();
    }

    private void RecreateRenderResources()
    {
        DestroyFrameGeneration();
        DestroyRayReconstruction();

        uint renderWidth = width;
        uint renderHeight = height;

        if (RayReconstruction is DLSSMode mode)
        {
            DLSSOptimalSettings settings = App.Context.GetDLSSRayReconstructionOptimalSettings(width, height, mode);

            if (settings.InputWidth is not 0 && settings.InputHeight is not 0)
            {
                renderWidth = settings.InputWidth;
                renderHeight = settings.InputHeight;
            }
        }

        pathTracingPass.Resize(renderWidth, renderHeight);

        if (RayReconstruction.HasValue)
        {
            CreateRayReconstruction();
        }

        if (GeneratedFrameCount is not 0)
        {
            CreateFrameGeneration();
        }

        ResetHistory();
    }

    private void CreateRayReconstruction()
    {
        reconstructedColor = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
        rayReconstruction = App.Context.CreateDLSSRayReconstruction(new()
        {
            InputWidth = pathTracingPass.Width,
            InputHeight = pathTracingPass.Height,
            OutputWidth = width,
            OutputHeight = height,
            Mode = RayReconstruction!.Value,
            Flags = DLSSRayReconstructionFlags.None
        });
    }

    private void DestroyRayReconstruction()
    {
        rayReconstruction?.Dispose();
        rayReconstruction = null;
        reconstructedColor?.Dispose();
        reconstructedColor = null;
    }

    private void CreateFrameGeneration()
    {
        TextureDesc desc = TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        };

        generatedColors = [.. Enumerable.Range(0, (int)GeneratedFrameCount).Select(_ => App.Context.CreateTexture(desc))];
        DisableInterpolation = App.Context.CreateBuffer(new()
        {
            SizeInBytes = sizeof(uint),
            Usages = BufferUsages.StorageReadWrite | BufferUsages.TransferSrc,
            Residency = MemoryResidency.GpuOnly
        });
        frameGeneration = App.Context.CreateDLSSFrameGeneration(new()
        {
            Format = PixelFormat.B8G8R8A8UNorm,
            InputWidth = pathTracingPass.Width,
            InputHeight = pathTracingPass.Height,
            OutputWidth = width,
            OutputHeight = height
        });
    }

    private void DestroyFrameGeneration()
    {
        frameGeneration?.Dispose();
        frameGeneration = null;
        DisableInterpolation?.Dispose();
        DisableInterpolation = null;

        foreach (Texture generatedColor in generatedColors)
        {
            generatedColor.Dispose();
        }

        generatedColors = [];
    }

    private void Reconstruct(CommandBuffer commandBuffer, CameraHandler camera, Vector2 jitter)
    {
        commandBuffer.Transition(reconstructedColor!, default, TextureLayout.Undefined, TextureLayout.Storage);
        rayReconstruction!.Dispatch(commandBuffer, new()
        {
            Color = pathTracingPass.Color,
            Depth = pathTracingPass.Depth,
            MotionVectors = pathTracingPass.MotionVectors,
            DiffuseAlbedo = pathTracingPass.DiffuseAlbedo,
            SpecularAlbedo = pathTracingPass.SpecularAlbedo,
            Normals = pathTracingPass.Normal,
            Roughness = pathTracingPass.Roughness,
            SpecularHitDistance = pathTracingPass.SpecularHitDistance,
            Output = reconstructedColor!,
            JitterOffset = jitter,
            MotionVectorScale = new(-0.5f * pathTracingPass.Width, 0.5f * pathTracingPass.Height),
            WorldToView = camera.View,
            ViewToClip = camera.Projection,
            Reset = !hasHistory
        });
        commandBuffer.Transition(reconstructedColor!, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    private void Generate(CommandBuffer commandBuffer, CameraHandler camera, Vector2 jitter)
    {
        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        Matrix4x4 viewProjection = view * projection;
        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);

        Matrix4x4 clipToPrevClip = viewProjection == previousViewProjection ? Matrix4x4.Identity : inverseProjection * (inverseView * previousViewProjection);
        Matrix4x4.Invert(clipToPrevClip, out Matrix4x4 prevClipToClip);

        DLSSFrameGenerationArgs args = new()
        {
            Color = tonemapPass.Color,
            Depth = pathTracingPass.Depth,
            MotionVectors = pathTracingPass.MotionVectors,
            OutputDisableInterpolation = DisableInterpolation,
            GeneratedFrameCount = GeneratedFrameCount,
            JitterOffset = new(2.0f * jitter.X / pathTracingPass.Width, -2.0f * jitter.Y / pathTracingPass.Height),
            MotionVectorScale = new(-0.5f * pathTracingPass.Width, 0.5f * pathTracingPass.Height),
            ViewToClip = projection,
            ClipToView = inverseProjection,
            ClipToPrevClip = clipToPrevClip,
            PrevClipToClip = prevClipToClip,
            CameraPosition = camera.Position,
            CameraUp = camera.Up,
            CameraRight = camera.Right,
            CameraForward = camera.Forward,
            CameraNear = camera.NearPlane,
            CameraFar = camera.FarPlane,
            CameraFovAngleVer = float.DegreesToRadians(camera.Fov),
            CameraAspectRatio = camera.AspectRatio,
            Reset = !hasHistory
        };

        for (uint i = 0; i < GeneratedFrameCount; i++)
        {
            Texture generatedColor = generatedColors[i];

            commandBuffer.Transition(generatedColor, default, TextureLayout.Undefined, TextureLayout.Storage);
            frameGeneration!.Dispatch(commandBuffer, args with { Output = generatedColor, GeneratedFrameIndex = i });
            commandBuffer.Transition(generatedColor, default, TextureLayout.Storage, TextureLayout.Sampled);
        }
    }

    private static float Halton(uint index, uint radix)
    {
        float result = 0.0f;
        float fraction = 1.0f;

        while (index is not 0)
        {
            fraction /= radix;
            result += fraction * (index % radix);
            index /= radix;
        }

        return result;
    }
}
