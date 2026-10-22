using System.Numerics;
using CornellBox.Handlers;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal class FrameGenerationPass : DisposableObject
{
    private const uint DisableInterpolationSizeInBytes = sizeof(uint);

    private DLSSFrameGeneration? dlss;
    private Buffer? disableInterpolation;
    private Buffer? readback;
    private Texture[] colors = [];
    private int[] outputIndices = [];
    private uint inputWidth;
    private uint inputHeight;
    private uint outputWidth;
    private uint outputHeight;
    private bool hasPendingReset;

    public FrameGenerationPass(uint inputWidth, uint inputHeight, uint outputWidth, uint outputHeight, FrameGenerationMode mode = FrameGenerationMode.X1)
    {
        this.inputWidth = inputWidth;
        this.inputHeight = inputHeight;
        this.outputWidth = outputWidth;
        this.outputHeight = outputHeight;
        Mode = mode;

        CreateResources();
    }

    public FrameGenerationMode Mode { get; }

    public ReadOnlySpan<Texture> Colors => colors;

    public ReadOnlySpan<int> ReadOutputIndices()
    {
        ReadOnlySpan<int> indices = outputIndices;

        switch (App.Technology)
        {
            case RenderingTechnology.DLSS:
                {
                    if (Mode is FrameGenerationMode.X1)
                    {
                        break;
                    }

                    unsafe
                    {
                        byte* flags = (byte*)readback!.Map();
                        int outputCount = 0;

                        try
                        {
                            for (int i = 0; i < colors.Length - 1; i++)
                            {
                                if (flags[(uint)i * DisableInterpolationSizeInBytes] is 0)
                                {
                                    outputIndices[outputCount++] = i;
                                }
                            }
                        }
                        finally
                        {
                            readback.Unmap();
                        }

                        outputIndices[outputCount++] = colors.Length - 1;

                        indices = outputIndices.AsSpan(0, outputCount);
                    }
                }
                break;

            case RenderingTechnology.MetalFX:
                break;
        }

        return indices;
    }

    public void Record(CommandBuffer commandBuffer, Texture color, PathTracingPass inputs, CameraHandler camera)
    {
        colors[^1] = color;

        switch (App.Technology)
        {
            case RenderingTechnology.DLSS:
                {
                    if (Mode is FrameGenerationMode.X1)
                    {
                        break;
                    }

                    Matrix4x4.Invert(inputs.Projection, out Matrix4x4 clipToView);
                    Matrix4x4.Invert(inputs.ViewProjection, out Matrix4x4 clipToWorld);
                    Matrix4x4 clipToPrevClip = clipToWorld * inputs.PreviousViewProjection;
                    Matrix4x4.Invert(clipToPrevClip, out Matrix4x4 prevClipToClip);

                    for (int i = 0; i < colors.Length - 1; i++)
                    {
                        Texture output = colors[i];
                        commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.Storage);

                        dlss!.Dispatch(commandBuffer, new()
                        {
                            Color = color,
                            Depth = inputs.Depth,
                            MotionVectors = inputs.MotionVectors,
                            Output = output,
                            OutputDisableInterpolation = disableInterpolation,
                            GeneratedFrameCount = (uint)(colors.Length - 1),
                            GeneratedFrameIndex = (uint)i,
                            JitterOffset = new(2.0f * inputs.JitterOffset.X / inputWidth, -2.0f * inputs.JitterOffset.Y / inputHeight),
                            MotionVectorScale = Vector2.One,
                            ViewToClip = inputs.Projection,
                            ClipToView = clipToView,
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
                            IsHdrDisplay = false,
                            IsDepthReversed = false,
                            IsOrthographic = false,
                            Reset = inputs.ResetHistory || hasPendingReset
                        });

                        commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);
                        commandBuffer.CopyBuffer(disableInterpolation!, 0, readback!, (uint)i * DisableInterpolationSizeInBytes, DisableInterpolationSizeInBytes);
                    }

                    hasPendingReset = false;
                }
                break;

            case RenderingTechnology.MetalFX:
                break;
        }
    }

    public void Resize(uint newInputWidth, uint newInputHeight, uint newOutputWidth, uint newOutputHeight)
    {
        if (inputWidth == newInputWidth && inputHeight == newInputHeight && outputWidth == newOutputWidth && outputHeight == newOutputHeight)
        {
            return;
        }

        DestroyResources();

        inputWidth = newInputWidth;
        inputHeight = newInputHeight;
        outputWidth = newOutputWidth;
        outputHeight = newOutputHeight;

        CreateResources();
    }

    protected override void Destroy()
    {
        DestroyResources();
    }

    private void CreateResources()
    {
        colors = new Texture[1];
        outputIndices = [0];

        switch (App.Technology)
        {
            case RenderingTechnology.DLSS:
                {
                    if (Mode is FrameGenerationMode.X1)
                    {
                        break;
                    }

                    int generatedFrameCount = (int)Mode;
                    colors = new Texture[generatedFrameCount + 1];
                    outputIndices = new int[colors.Length];

                    dlss = App.Context.CreateDLSSFrameGeneration(new()
                    {
                        Format = PixelFormat.R8G8B8A8UNorm,
                        InputWidth = inputWidth,
                        InputHeight = inputHeight,
                        OutputWidth = outputWidth,
                        OutputHeight = outputHeight
                    });

                    for (int i = 0; i < generatedFrameCount; i++)
                    {
                        colors[i] = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R8G8B8A8UNorm, outputWidth, outputHeight, 1, SampleCount.Count1) with
                        {
                            Usages = TextureUsages.Sampled | TextureUsages.Storage
                        });
                    }

                    disableInterpolation = App.Context.CreateBuffer(new()
                    {
                        SizeInBytes = DisableInterpolationSizeInBytes,
                        Usages = BufferUsages.StorageReadWrite | BufferUsages.TransferSrc,
                        Residency = MemoryResidency.GpuOnly
                    });
                    readback = App.Context.CreateBuffer(new()
                    {
                        SizeInBytes = DisableInterpolationSizeInBytes * (uint)generatedFrameCount,
                        Usages = BufferUsages.TransferDst,
                        Residency = MemoryResidency.CpuReadOnly
                    });
                }
                break;

            case RenderingTechnology.MetalFX:
                break;
        }

        hasPendingReset = true;
    }

    private void DestroyResources()
    {
        readback?.Dispose();
        readback = null;
        disableInterpolation?.Dispose();
        disableInterpolation = null;

        for (int i = colors.Length - 2; i >= 0; i--)
        {
            colors[i]?.Dispose();
        }
        colors = [];
        outputIndices = [];

        dlss?.Dispose();
        dlss = null;
    }
}
