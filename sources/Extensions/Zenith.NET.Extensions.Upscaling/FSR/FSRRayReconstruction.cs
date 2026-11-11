using System.Numerics;
using System.Runtime.InteropServices;
using FSR.NET;
using Buffer = Zenith.NET.Buffer;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal unsafe partial class FSRRayReconstruction : RayReconstruction
{
    private readonly DenoisingPass denoisingPass;
    private readonly CompositionPass compositionPass;
    private readonly FSRSuperResolution superResolution;

    private Vector3 previousCameraPosition;
    private uint frameIndex;
    private bool hasHistory;

    public FSRRayReconstruction(FSRVendor vendor, RayReconstructionDesc desc) : base(desc)
    {
        denoisingPass = new(vendor, desc);
        compositionPass = new(vendor.Context, desc);
        superResolution = new(vendor, new()
        {
            RenderWidth = desc.RenderWidth,
            RenderHeight = desc.RenderHeight,
            OutputWidth = desc.OutputWidth,
            OutputHeight = desc.OutputHeight
        });
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        Matrix4x4.Invert(args.View, out Matrix4x4 viewToWorld);

        bool reset = !hasHistory || args.Reset;
        Vector3 cameraPosition = viewToWorld.Translation;
        Vector3 cameraPositionDelta = reset ? Vector3.Zero : previousCameraPosition - cameraPosition;

        denoisingPass.Dispatch(commandBuffer, args, cameraPositionDelta, frameIndex, reset);
        compositionPass.Dispatch(commandBuffer, args, denoisingPass.DiffuseRadiance, denoisingPass.SpecularRadiance, denoisingPass.DenoisedDiffuse, denoisingPass.DenoisedSpecular, frameIndex);

        superResolution.Dispatch(commandBuffer, new()
        {
            Color = compositionPass.Color,
            Depth = args.Depth,
            MotionVectors = args.MotionVectors,
            ReactiveMask = args.ReactiveMask,
            Exposure = args.Exposure,
            Output = args.Output,
            JitterOffset = args.JitterOffset,
            Projection = args.Projection,
            DeltaTimeInSeconds = args.DeltaTimeInSeconds,
            PreExposure = args.PreExposure,
            Reset = reset
        }, compositionPass.TransparencyMask);

        previousCameraPosition = cameraPosition;
        hasHistory = true;
        frameIndex++;
    }

    protected override void Destroy()
    {
        superResolution.Dispose();
        compositionPass.Dispose();
        denoisingPass.Dispose();
    }

    private static Texture CreateTexture(GraphicsContext context, PixelFormat format, RayReconstructionDesc desc, TextureUsages additionalUsages = TextureUsages.None)
    {
        return context.CreateTexture(TextureDesc.Texture2D(format, desc.RenderWidth, desc.RenderHeight, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage | additionalUsages
        });
    }

    private static ComputePipeline CreatePipeline(GraphicsContext context, ShaderDesc desc)
    {
        using Shader shader = context.CreateShader(desc);

        return context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    private class DenoisingPass : DisposableObject
    {
        private readonly FSRVendor vendor;
        private readonly RayReconstructionDesc desc;
        private readonly FSRContext context;
        private readonly Buffer constantBuffer;
        private readonly ComputePipeline pipeline;
        private readonly Texture previousLinearDepth;
        private readonly Texture linearDepth;
        private readonly Texture motionVectors;
        private readonly Texture normals;

        public readonly Texture DiffuseRadiance;

        public readonly Texture SpecularRadiance;

        public readonly Texture DenoisedDiffuse;

        public readonly Texture DenoisedSpecular;

        public DenoisingPass(FSRVendor vendor, RayReconstructionDesc desc)
        {
            this.vendor = vendor;
            this.desc = desc;

            const FSRApiDenoiserSignalFlags SignalFlags = FSRApiDenoiserSignalFlags.IndirectDiffuse | FSRApiDenoiserSignalFlags.IndirectSpecular;

            FSRCreateContextDescDenoiser createDesc = new()
            {
                Version = (uint)Fsr.DenoiserVersion,
                MaxRenderSize = new()
                {
                    Width = desc.RenderWidth,
                    Height = desc.RenderHeight
                },
                SignalFlags = (uint)SignalFlags
            };
            context = vendor.CreateContext(&createDesc.Header);

            GraphicsContext graphicsContext = vendor.Context;
            constantBuffer = graphicsContext.CreateBuffer(BufferDesc.Constant((uint)sizeof(PreparationConstants)));
            pipeline = CreatePipeline(graphicsContext, DirectX12Prepare);
            previousLinearDepth = graphicsContext.CreateTexture(TextureDesc.Texture2D(PixelFormat.R32Float, desc.RenderWidth, desc.RenderHeight, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.Sampled | TextureUsages.TransferDst
            });
            linearDepth = CreateTexture(graphicsContext, PixelFormat.R32Float, desc, TextureUsages.TransferSrc);
            motionVectors = CreateTexture(graphicsContext, PixelFormat.R32G32B32A32Float, desc);
            normals = CreateTexture(graphicsContext, PixelFormat.R16G16B16A16Float, desc);
            DiffuseRadiance = CreateTexture(graphicsContext, PixelFormat.R16G16B16A16Float, desc);
            SpecularRadiance = CreateTexture(graphicsContext, PixelFormat.R16G16B16A16Float, desc);
            DenoisedDiffuse = CreateTexture(graphicsContext, PixelFormat.R32G32B32A32Float, desc);
            DenoisedSpecular = CreateTexture(graphicsContext, PixelFormat.R32G32B32A32Float, desc);
        }

        public void Dispatch(CommandBuffer commandBuffer, RayReconstructionArgs args, Vector3 cameraPositionDelta, uint frameIndex, bool reset)
        {
            Prepare(commandBuffer, args, frameIndex, reset);

            commandBuffer.Transition(args.SpecularAlbedo, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.DiffuseAlbedo, default, TextureLayout.Sampled, TextureLayout.Common);

            TextureLayout denoisedBefore = frameIndex is 0 ? TextureLayout.Undefined : TextureLayout.Sampled;
            commandBuffer.Transition(DenoisedDiffuse, default, denoisedBefore, TextureLayout.Common);
            commandBuffer.Transition(DenoisedSpecular, default, denoisedBefore, TextureLayout.Common);

            (float nearPlane, float farPlane, _, _, _, bool depthInfinite) = Vendor.DecomposePerspective(args.Projection);
            float far = depthInfinite ? float.MaxValue : farPlane;

            FSRDispatchDescDenoiser dispatchDesc = new()
            {
                CommandList = commandBuffer.FSR(),
                LinearDepth = linearDepth.FSR(),
                MotionVectors = motionVectors.FSR(),
                Normals = normals.FSR(),
                SpecularAlbedo = args.SpecularAlbedo.FSR(),
                DiffuseAlbedo = args.DiffuseAlbedo.FSR(),
                MotionVectorScale = new()
                {
                    X = desc.RenderWidth,
                    Y = desc.RenderHeight,
                    Z = 1.0f
                },
                JitterOffsets = new()
                {
                    X = -args.JitterOffset.X,
                    Y = -args.JitterOffset.Y
                },
                CameraPositionDelta = new()
                {
                    X = cameraPositionDelta.X,
                    Y = cameraPositionDelta.Y,
                    Z = cameraPositionDelta.Z
                },
                View = FSRFormats.FSR(args.View),
                Projection = FSRFormats.FSR(args.Projection),
                LinearDepthBounds = new()
                {
                    Min = nearPlane,
                    Max = far
                },
                RenderSize = new()
                {
                    Width = desc.RenderWidth,
                    Height = desc.RenderHeight
                },
                FrameIndex = frameIndex,
                Flags = (uint)(FSRApiDispatchDenoiserFlags.NonEncodedAlbedo | (reset ? FSRApiDispatchDenoiserFlags.Reset : 0))
            };

            FSRDispatchDescDenoiserIndirectDiffuse indirectDiffuse = new()
            {
                Signal = new()
                {
                    Input = DiffuseRadiance.FSR(),
                    Output = DenoisedDiffuse.FSR()
                }
            };
            FSRDispatchDescDenoiserIndirectSpecular indirectSpecular = new()
            {
                Signal = new()
                {
                    Input = SpecularRadiance.FSR(),
                    Output = DenoisedSpecular.FSR()
                }
            };

            dispatchDesc.Header.Next = &indirectDiffuse.Header;
            indirectDiffuse.Header.Next = &indirectSpecular.Header;

            commandBuffer.BeginExternalCommands();

            vendor.Dispatch(context, &dispatchDesc.Header);

            commandBuffer.EndExternalCommands();

            commandBuffer.Transition(args.SpecularAlbedo, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.DiffuseAlbedo, default, TextureLayout.Common, TextureLayout.Sampled);

            commandBuffer.Transition(linearDepth, default, TextureLayout.Common, TextureLayout.CopySrc);
            commandBuffer.Transition(previousLinearDepth, default, TextureLayout.Sampled, TextureLayout.CopyDst);
            commandBuffer.CopyTexture(linearDepth, default, default, previousLinearDepth, default, default, new()
            {
                Width = desc.RenderWidth,
                Height = desc.RenderHeight,
                Depth = 1
            });
            commandBuffer.Transition(previousLinearDepth, default, TextureLayout.CopyDst, TextureLayout.Sampled);
            commandBuffer.Transition(linearDepth, default, TextureLayout.CopySrc, TextureLayout.Common);

            commandBuffer.Transition(DiffuseRadiance, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(SpecularRadiance, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(DenoisedDiffuse, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(DenoisedSpecular, default, TextureLayout.Common, TextureLayout.Sampled);
        }

        protected override void Destroy()
        {
            DenoisedSpecular.Dispose();
            DenoisedDiffuse.Dispose();
            SpecularRadiance.Dispose();
            DiffuseRadiance.Dispose();
            normals.Dispose();
            motionVectors.Dispose();
            linearDepth.Dispose();
            previousLinearDepth.Dispose();
            pipeline.Dispose();
            constantBuffer.Dispose();
            vendor.DestroyContext(context);
        }

        private void Prepare(CommandBuffer commandBuffer, RayReconstructionArgs args, uint frameIndex, bool reset)
        {
            PreparationConstants constants = new()
            {
                Width = desc.RenderWidth,
                Height = desc.RenderHeight,
                HasHistory = reset ? 0u : 1u,
                ProjectionDepth = new(args.Projection.M33, args.Projection.M34, args.Projection.M43, args.Projection.M44),
                Color = args.Color.SampledHandle,
                Depth = args.Depth.SampledHandle,
                MotionVectors = args.MotionVectors.SampledHandle,
                DiffuseAlbedo = args.DiffuseAlbedo.SampledHandle,
                SpecularAlbedo = args.SpecularAlbedo.SampledHandle,
                Normals = args.Normals.SampledHandle,
                Roughness = args.Roughness.SampledHandle,
                SpecularHitDistance = args.SpecularHitDistance.SampledHandle,
                PreviousLinearDepth = previousLinearDepth.SampledHandle,
                LinearDepth = linearDepth.StorageHandle,
                PackedMotion = motionVectors.StorageHandle,
                NormalRoughness = normals.StorageHandle,
                DiffuseRadiance = DiffuseRadiance.StorageHandle,
                SpecularRadiance = SpecularRadiance.StorageHandle
            };

            commandBuffer.Upload(constantBuffer, 0, new()
            {
                Pointer = (nint)(&constants),
                SizeInBytes = (uint)sizeof(PreparationConstants)
            });
            commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.ComputeShading);

            if (frameIndex is 0)
            {
                commandBuffer.Transition(previousLinearDepth, default, TextureLayout.Undefined, TextureLayout.Sampled);
            }

            TextureLayout scratchBefore = frameIndex is 0 ? TextureLayout.Undefined : TextureLayout.Common;
            TextureLayout signalBefore = frameIndex is 0 ? TextureLayout.Undefined : TextureLayout.Sampled;
            commandBuffer.Transition(linearDepth, default, scratchBefore, TextureLayout.Storage);
            commandBuffer.Transition(motionVectors, default, scratchBefore, TextureLayout.Storage);
            commandBuffer.Transition(normals, default, scratchBefore, TextureLayout.Storage);
            commandBuffer.Transition(DiffuseRadiance, default, signalBefore, TextureLayout.Storage);
            commandBuffer.Transition(SpecularRadiance, default, signalBefore, TextureLayout.Storage);

            commandBuffer.SetPipeline(pipeline);
            commandBuffer.SetConstantBuffer(constantBuffer, 0);
            commandBuffer.Dispatch((desc.RenderWidth + 7) / 8, (desc.RenderHeight + 7) / 8, 1);

            commandBuffer.Transition(linearDepth, default, TextureLayout.Storage, TextureLayout.Common);
            commandBuffer.Transition(motionVectors, default, TextureLayout.Storage, TextureLayout.Common);
            commandBuffer.Transition(normals, default, TextureLayout.Storage, TextureLayout.Common);
            commandBuffer.Transition(DiffuseRadiance, default, TextureLayout.Storage, TextureLayout.Common);
            commandBuffer.Transition(SpecularRadiance, default, TextureLayout.Storage, TextureLayout.Common);
        }
    }

    private class CompositionPass(GraphicsContext context, RayReconstructionDesc desc) : DisposableObject
    {
        private readonly Buffer constantBuffer = context.CreateBuffer(BufferDesc.Constant((uint)sizeof(CompositionConstants)));
        private readonly ComputePipeline pipeline = CreatePipeline(context, DirectX12Compose);

        public readonly Texture Color = CreateTexture(context, PixelFormat.R16G16B16A16Float, desc);

        private bool transparencyMaskInitialized;

        public Texture? TransparencyMask { get; private set; }

        public void Dispatch(CommandBuffer commandBuffer,
                             RayReconstructionArgs args,
                             Texture diffuseRadiance,
                             Texture specularRadiance,
                             Texture denoisedDiffuse,
                             Texture denoisedSpecular,
                             uint frameIndex)
        {
            bool hasTransparencyOverlay = args.TransparencyOverlay is not null;

            if (hasTransparencyOverlay && TransparencyMask is null)
            {
                TransparencyMask = CreateTexture(context, PixelFormat.R8UNorm, desc);
                transparencyMaskInitialized = false;
            }

            CompositionConstants constants = new()
            {
                Width = desc.RenderWidth,
                Height = desc.RenderHeight,
                HasTransparencyOverlay = hasTransparencyOverlay ? 1u : 0u,
                Color = args.Color.SampledHandle,
                DiffuseAlbedo = args.DiffuseAlbedo.SampledHandle,
                SpecularAlbedo = args.SpecularAlbedo.SampledHandle,
                TransparencyOverlay = args.TransparencyOverlay?.SampledHandle ?? default,
                DiffuseRadiance = diffuseRadiance.SampledHandle,
                SpecularRadiance = specularRadiance.SampledHandle,
                DenoisedDiffuse = denoisedDiffuse.SampledHandle,
                DenoisedSpecular = denoisedSpecular.SampledHandle,
                CompositedColor = Color.StorageHandle,
                TransparencyMask = hasTransparencyOverlay ? TransparencyMask!.StorageHandle : default
            };

            commandBuffer.Upload(constantBuffer, 0, new()
            {
                Pointer = (nint)(&constants),
                SizeInBytes = (uint)sizeof(CompositionConstants)
            });
            commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.ComputeShading);

            commandBuffer.Transition(Color, default, frameIndex is 0 ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Storage);

            if (hasTransparencyOverlay)
            {
                commandBuffer.Transition(TransparencyMask!, default, transparencyMaskInitialized ? TextureLayout.Sampled : TextureLayout.Undefined, TextureLayout.Storage);
            }

            commandBuffer.SetPipeline(pipeline);
            commandBuffer.SetConstantBuffer(constantBuffer, 0);
            commandBuffer.Dispatch((desc.RenderWidth + 7) / 8, (desc.RenderHeight + 7) / 8, 1);

            commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);

            if (hasTransparencyOverlay)
            {
                commandBuffer.Transition(TransparencyMask!, default, TextureLayout.Storage, TextureLayout.Sampled);
                transparencyMaskInitialized = true;
            }
        }

        protected override void Destroy()
        {
            TransparencyMask?.Dispose();
            Color.Dispose();
            pipeline.Dispose();
            constantBuffer.Dispose();
        }
    }
}

[StructLayout(LayoutKind.Explicit, Size = 144)]
file struct PreparationConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(8)]
    public uint HasHistory;

    [FieldOffset(16)]
    public Vector4 ProjectionDepth;

    [FieldOffset(32)]
    public ResourceHandle Color;

    [FieldOffset(40)]
    public ResourceHandle Depth;

    [FieldOffset(48)]
    public ResourceHandle MotionVectors;

    [FieldOffset(56)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(64)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(72)]
    public ResourceHandle Normals;

    [FieldOffset(80)]
    public ResourceHandle Roughness;

    [FieldOffset(88)]
    public ResourceHandle SpecularHitDistance;

    [FieldOffset(96)]
    public ResourceHandle PreviousLinearDepth;

    [FieldOffset(104)]
    public ResourceHandle LinearDepth;

    [FieldOffset(112)]
    public ResourceHandle PackedMotion;

    [FieldOffset(120)]
    public ResourceHandle NormalRoughness;

    [FieldOffset(128)]
    public ResourceHandle DiffuseRadiance;

    [FieldOffset(136)]
    public ResourceHandle SpecularRadiance;
}

[StructLayout(LayoutKind.Explicit, Size = 96)]
file struct CompositionConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(8)]
    public uint HasTransparencyOverlay;

    [FieldOffset(16)]
    public ResourceHandle Color;

    [FieldOffset(24)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(32)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(40)]
    public ResourceHandle TransparencyOverlay;

    [FieldOffset(48)]
    public ResourceHandle DiffuseRadiance;

    [FieldOffset(56)]
    public ResourceHandle SpecularRadiance;

    [FieldOffset(64)]
    public ResourceHandle DenoisedDiffuse;

    [FieldOffset(72)]
    public ResourceHandle DenoisedSpecular;

    [FieldOffset(80)]
    public ResourceHandle CompositedColor;

    [FieldOffset(88)]
    public ResourceHandle TransparencyMask;
}
