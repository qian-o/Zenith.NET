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

    public FSRRayReconstruction(FSRVendor vendor, RayReconstructionDesc desc) : base(desc)
    {
        denoisingPass = new(vendor, desc);
        compositionPass = new(vendor.Context, desc, denoisingPass.SignalFlags);
        superResolution = new(vendor, new()
        {
            ColorFormat = compositionPass.Color.Desc.Format,
            DepthFormat = desc.DepthFormat,
            MotionVectorsFormat = desc.MotionVectorsFormat,
            ReactiveMaskFormat = desc.ReactiveMaskFormat,
            OutputFormat = desc.OutputFormat,
            IsHdr = desc.IsHdr,
            InputWidth = desc.InputWidth,
            InputHeight = desc.InputHeight,
            OutputWidth = desc.OutputWidth,
            OutputHeight = desc.OutputHeight,
            Flags = desc.Flags
        });
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        Matrix4x4.Invert(args.WorldToView, out Matrix4x4 viewToWorld);

        Vector3 cameraPosition = viewToWorld.Translation;
        Vector3 cameraPositionDelta = args.Reset ? Vector3.Zero : previousCameraPosition - cameraPosition;

        denoisingPass.Dispatch(commandBuffer, args, cameraPositionDelta, frameIndex);
        compositionPass.Dispatch(commandBuffer, args, denoisingPass.SignalOutputs, frameIndex);

        superResolution.Dispatch(commandBuffer, new()
        {
            Color = compositionPass.Color,
            Depth = args.Depth,
            MotionVectors = args.MotionVectors,
            ReactiveMask = args.ReactiveMask,
            Exposure = args.Exposure,
            Output = args.Output,
            JitterOffset = args.JitterOffset,
            WorldToView = args.WorldToView,
            ViewToClip = args.ViewToClip,
            DeltaTimeInSeconds = args.DeltaTimeInSeconds,
            PreExposure = args.PreExposure,
            Reset = args.Reset
        }, compositionPass.TransparencyMask);

        previousCameraPosition = cameraPosition;
        frameIndex++;
    }

    protected override void Destroy()
    {
        superResolution.Dispose();
        compositionPass.Dispose();
        denoisingPass.Dispose();
    }

    private static Texture CreateTexture(GraphicsContext context, PixelFormat format, RayReconstructionDesc desc)
    {
        return context.CreateTexture(TextureDesc.Texture2D(format, desc.InputWidth, desc.InputHeight, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
    }

    private static ComputePipeline CreatePipeline(GraphicsContext context, ShaderDesc desc)
    {
        using Shader shader = context.CreateShader(desc);

        return context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    private class DenoisingPass : DisposableObject
    {
        private readonly Buffer constantBuffer;
        private readonly ComputePipeline pipeline;
        private readonly Texture linearDepth;
        private readonly Texture motionVectors;
        private readonly Texture normals;

        public readonly FSRVendor Vendor;

        public readonly Texture?[] SignalOutputs = new Texture?[4];

        public FSRContext Context;

        public DenoisingPass(FSRVendor vendor, RayReconstructionDesc desc)
        {
            Vendor = vendor;
            Desc = desc;

            FSRApiDenoiserSignalFlags signalFlags = FSRApiDenoiserSignalFlags.None;

            if (desc.DirectDiffuseRadianceFormat is not null)
            {
                signalFlags |= FSRApiDenoiserSignalFlags.DirectDiffuse;
            }

            if (desc.DirectSpecularRadianceFormat is not null)
            {
                signalFlags |= FSRApiDenoiserSignalFlags.DirectSpecular;
            }

            if (desc.IndirectDiffuseRadianceFormat is not null)
            {
                signalFlags |= FSRApiDenoiserSignalFlags.IndirectDiffuse;
            }

            if (desc.IndirectSpecularRadianceFormat is not null)
            {
                signalFlags |= FSRApiDenoiserSignalFlags.IndirectSpecular;
            }

            SignalFlags = signalFlags;

            FSRCreateContextDescDenoiser createDesc = new()
            {
                Version = (uint)Fsr.DenoiserVersion,
                MaxRenderSize = new()
                {
                    Width = desc.InputWidth,
                    Height = desc.InputHeight
                },
                SignalFlags = (uint)signalFlags
            };
            vendor.CreateContext(ref Context, &createDesc.Header);

            GraphicsContext graphicsContext = vendor.Context;
            constantBuffer = graphicsContext.CreateBuffer(new()
            {
                SizeInBytes = (uint)sizeof(PreparationConstants),
                Usages = BufferUsages.Constant,
                Residency = MemoryResidency.CpuWriteOnly
            });
            pipeline = CreatePipeline(graphicsContext, DirectX12Prepare);
            linearDepth = CreateTexture(graphicsContext, PixelFormat.R32Float, desc);
            motionVectors = CreateTexture(graphicsContext, PixelFormat.R32G32B32A32Float, desc);
            normals = CreateTexture(graphicsContext, PixelFormat.R16G16B16A16Float, desc);

            ReadOnlySpan<FSRApiDenoiserSignalFlags> signals =
            [
                FSRApiDenoiserSignalFlags.DirectDiffuse,
                FSRApiDenoiserSignalFlags.DirectSpecular,
                FSRApiDenoiserSignalFlags.IndirectDiffuse,
                FSRApiDenoiserSignalFlags.IndirectSpecular
            ];
            for (int i = 0; i < signals.Length; i++)
            {
                if (signalFlags.HasFlag(signals[i]))
                {
                    SignalOutputs[i] = CreateTexture(graphicsContext, PixelFormat.R32G32B32A32Float, desc);
                }
            }
        }

        public RayReconstructionDesc Desc { get; }

        public FSRApiDenoiserSignalFlags SignalFlags { get; }

        public void Dispatch(CommandBuffer commandBuffer, RayReconstructionArgs args, Vector3 cameraPositionDelta, uint frameIndex)
        {
            Prepare(commandBuffer, args, frameIndex);

            foreach (Texture? signal in SignalOutputs)
            {
                if (signal is not null)
                {
                    commandBuffer.Transition(signal, default, frameIndex is 0 ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Storage);
                }
            }

            (float nearPlane, float farPlane, _, _) = UpscalingHelper.DecomposePerspective(args.ViewToClip);
            float far = Desc.Flags.HasFlag(UpscalingInputFlags.DepthInfinite) ? float.MaxValue : farPlane;

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
                    X = 1.0f,
                    Y = 1.0f,
                    Z = 1.0f
                },
                JitterOffsets = new()
                {
                    X = args.JitterOffset.X,
                    Y = args.JitterOffset.Y
                },
                CameraPositionDelta = new()
                {
                    X = cameraPositionDelta.X,
                    Y = cameraPositionDelta.Y,
                    Z = cameraPositionDelta.Z
                },
                View = FSRFormats.FSR(args.WorldToView),
                Projection = FSRFormats.FSR(args.ViewToClip),
                LinearDepthBounds = new()
                {
                    Min = nearPlane,
                    Max = far
                },
                RenderSize = new()
                {
                    Width = Desc.InputWidth,
                    Height = Desc.InputHeight
                },
                FrameIndex = frameIndex,
                Flags = (uint)(FSRApiDispatchDenoiserFlags.NonEncodedAlbedo | (args.Reset ? FSRApiDispatchDenoiserFlags.Reset : 0))
            };
            FSRApiHeader* tail = &dispatchDesc.Header;
            FSRDispatchDescDenoiserDirectDiffuse directDiffuse = new();
            FSRDispatchDescDenoiserDirectSpecular directSpecular = new();
            FSRDispatchDescDenoiserIndirectDiffuse indirectDiffuse = new();
            FSRDispatchDescDenoiserIndirectSpecular indirectSpecular = new();

            if (SignalOutputs[0] is Texture directDiffuseOutput)
            {
                directDiffuse.Signal = new()
                {
                    Input = args.DirectDiffuseRadiance.FSR(),
                    Output = directDiffuseOutput.FSR(FSRApiResourceState.UnorderedAccess)
                };
                tail->Next = &directDiffuse.Header;
                tail = &directDiffuse.Header;
            }

            if (SignalOutputs[1] is Texture directSpecularOutput)
            {
                directSpecular.Signal = new()
                {
                    Input = args.DirectSpecularRadiance.FSR(),
                    Output = directSpecularOutput.FSR(FSRApiResourceState.UnorderedAccess)
                };
                tail->Next = &directSpecular.Header;
                tail = &directSpecular.Header;
            }

            if (SignalOutputs[2] is Texture indirectDiffuseOutput)
            {
                indirectDiffuse.Signal = new()
                {
                    Input = args.IndirectDiffuseRadiance.FSR(),
                    Output = indirectDiffuseOutput.FSR(FSRApiResourceState.UnorderedAccess)
                };
                tail->Next = &indirectDiffuse.Header;
                tail = &indirectDiffuse.Header;
            }

            if (SignalOutputs[3] is Texture indirectSpecularOutput)
            {
                indirectSpecular.Signal = new()
                {
                    Input = args.IndirectSpecularRadiance.FSR(),
                    Output = indirectSpecularOutput.FSR(FSRApiResourceState.UnorderedAccess)
                };
                tail->Next = &indirectSpecular.Header;
            }

            Vendor.Dispatch(ref Context, &dispatchDesc.Header);
        }

        protected override void Destroy()
        {
            for (int i = 0; i < SignalOutputs.Length; i++)
            {
                SignalOutputs[i]?.Dispose();
            }

            normals.Dispose();
            motionVectors.Dispose();
            linearDepth.Dispose();
            pipeline.Dispose();
            constantBuffer.Dispose();
            Vendor.DestroyContext(ref Context);
        }

        private void Prepare(CommandBuffer commandBuffer, RayReconstructionArgs args, uint frameIndex)
        {
            PreparationConstants constants = new()
            {
                Width = Desc.InputWidth,
                Height = Desc.InputHeight,
                SignalFlags = (uint)SignalFlags,
                ProjectionDepth = new(args.ViewToClip.M33, args.ViewToClip.M34, args.ViewToClip.M43, args.ViewToClip.M44),
                Depth = args.Depth.SampledHandle,
                MotionVectors = args.MotionVectors.SampledHandle,
                Normals = args.Normals.SampledHandle,
                Roughness = args.Roughness.SampledHandle,
                LinearDepthDelta = args.LinearDepthDelta!.SampledHandle,
                LinearDepth = linearDepth.StorageHandle,
                PackedMotion = motionVectors.StorageHandle,
                NormalRoughness = normals.StorageHandle
            };

            constantBuffer.Upload(0, new()
            {
                Pointer = (nint)(&constants),
                SizeInBytes = (uint)sizeof(PreparationConstants)
            });

            TextureLayout before = frameIndex is 0 ? TextureLayout.Undefined : TextureLayout.Sampled;
            commandBuffer.Transition(linearDepth, default, before, TextureLayout.Storage);
            commandBuffer.Transition(motionVectors, default, before, TextureLayout.Storage);
            commandBuffer.Transition(normals, default, before, TextureLayout.Storage);

            commandBuffer.SetPipeline(pipeline);
            commandBuffer.SetConstantBuffer(constantBuffer, 0);
            commandBuffer.Dispatch((Desc.InputWidth + 7) / 8, (Desc.InputHeight + 7) / 8, 1);

            commandBuffer.Transition(linearDepth, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Transition(motionVectors, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Transition(normals, default, TextureLayout.Storage, TextureLayout.Sampled);
        }
    }

    private class CompositionPass : DisposableObject
    {
        private readonly Buffer constantBuffer;
        private readonly ComputePipeline pipeline;

        public readonly Texture Color;

        public readonly Texture? TransparencyMask;

        public CompositionPass(GraphicsContext context, RayReconstructionDesc desc, FSRApiDenoiserSignalFlags signalFlags)
        {
            Desc = desc;
            SignalFlags = signalFlags;

            constantBuffer = context.CreateBuffer(new()
            {
                SizeInBytes = (uint)sizeof(CompositionConstants),
                Usages = BufferUsages.Constant,
                Residency = MemoryResidency.CpuWriteOnly
            });
            pipeline = CreatePipeline(context, DirectX12Compose);
            Color = CreateTexture(context, PixelFormat.R16G16B16A16Float, desc);

            if (desc.TransparencyOverlayFormat is not null)
            {
                TransparencyMask = CreateTexture(context, PixelFormat.R8UNorm, desc);
            }
        }

        public RayReconstructionDesc Desc { get; }

        public FSRApiDenoiserSignalFlags SignalFlags { get; }

        public void Dispatch(CommandBuffer commandBuffer, RayReconstructionArgs args, ReadOnlySpan<Texture?> signalOutputs, uint frameIndex)
        {
            CompositionConstants constants = new()
            {
                Width = Desc.InputWidth,
                Height = Desc.InputHeight,
                SignalFlags = (uint)SignalFlags,
                HasTransparencyOverlay = TransparencyMask is not null ? 1u : 0u,
                Color = args.Color.SampledHandle,
                DiffuseAlbedo = args.DiffuseAlbedo.SampledHandle,
                SpecularAlbedo = args.SpecularAlbedo.SampledHandle,
                TransparencyOverlay = args.TransparencyOverlay?.SampledHandle ?? default,
                DirectDiffuse = args.DirectDiffuseRadiance?.SampledHandle ?? default,
                DirectSpecular = args.DirectSpecularRadiance?.SampledHandle ?? default,
                IndirectDiffuse = args.IndirectDiffuseRadiance?.SampledHandle ?? default,
                IndirectSpecular = args.IndirectSpecularRadiance?.SampledHandle ?? default,
                DenoisedDirectDiffuse = signalOutputs[0]?.SampledHandle ?? default,
                DenoisedDirectSpecular = signalOutputs[1]?.SampledHandle ?? default,
                DenoisedIndirectDiffuse = signalOutputs[2]?.SampledHandle ?? default,
                DenoisedIndirectSpecular = signalOutputs[3]?.SampledHandle ?? default,
                CompositedColor = Color.StorageHandle,
                TransparencyMask = TransparencyMask?.StorageHandle ?? default
            };

            constantBuffer.Upload(0, new()
            {
                Pointer = (nint)(&constants),
                SizeInBytes = (uint)sizeof(CompositionConstants)
            });

            foreach (Texture? signal in signalOutputs)
            {
                if (signal is not null)
                {
                    commandBuffer.Transition(signal, default, TextureLayout.Storage, TextureLayout.Sampled);
                }
            }

            TextureLayout before = frameIndex is 0 ? TextureLayout.Undefined : TextureLayout.Sampled;
            commandBuffer.Transition(Color, default, before, TextureLayout.Storage);

            if (TransparencyMask is not null)
            {
                commandBuffer.Transition(TransparencyMask, default, before, TextureLayout.Storage);
            }

            commandBuffer.SetPipeline(pipeline);
            commandBuffer.SetConstantBuffer(constantBuffer, 0);
            commandBuffer.Dispatch((Desc.InputWidth + 7) / 8, (Desc.InputHeight + 7) / 8, 1);

            commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);

            if (TransparencyMask is not null)
            {
                commandBuffer.Transition(TransparencyMask, default, TextureLayout.Storage, TextureLayout.Sampled);
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

[StructLayout(LayoutKind.Explicit, Size = 96)]
file struct PreparationConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(8)]
    public uint SignalFlags;

    [FieldOffset(16)]
    public Vector4 ProjectionDepth;

    [FieldOffset(32)]
    public ResourceHandle Depth;

    [FieldOffset(40)]
    public ResourceHandle MotionVectors;

    [FieldOffset(48)]
    public ResourceHandle Normals;

    [FieldOffset(56)]
    public ResourceHandle Roughness;

    [FieldOffset(64)]
    public ResourceHandle LinearDepthDelta;

    [FieldOffset(72)]
    public ResourceHandle LinearDepth;

    [FieldOffset(80)]
    public ResourceHandle PackedMotion;

    [FieldOffset(88)]
    public ResourceHandle NormalRoughness;
}

[StructLayout(LayoutKind.Explicit, Size = 128)]
file struct CompositionConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(8)]
    public uint SignalFlags;

    [FieldOffset(12)]
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
    public ResourceHandle DirectDiffuse;

    [FieldOffset(56)]
    public ResourceHandle DirectSpecular;

    [FieldOffset(64)]
    public ResourceHandle IndirectDiffuse;

    [FieldOffset(72)]
    public ResourceHandle IndirectSpecular;

    [FieldOffset(80)]
    public ResourceHandle DenoisedDirectDiffuse;

    [FieldOffset(88)]
    public ResourceHandle DenoisedDirectSpecular;

    [FieldOffset(96)]
    public ResourceHandle DenoisedIndirectDiffuse;

    [FieldOffset(104)]
    public ResourceHandle DenoisedIndirectSpecular;

    [FieldOffset(112)]
    public ResourceHandle CompositedColor;

    [FieldOffset(120)]
    public ResourceHandle TransparencyMask;
}
