using System.Numerics;
using System.Runtime.InteropServices;
using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal unsafe class FSRRayReconstruction(FSRVendor vendor, RayReconstructionDesc desc) : RayReconstruction(vendor.Context, desc)
{
    private readonly Buffer prepareBuffer = vendor.Context.CreateBuffer(BufferDesc.Constant((uint)sizeof(PrepareConstants)));
    private readonly Buffer composeBuffer = vendor.Context.CreateBuffer(BufferDesc.Constant((uint)sizeof(ComposeConstants)));
    private readonly ComputePipeline preparePipeline = CreatePipeline(vendor.Context, UpscalingBridge.RayReconstructionPrepareCS(vendor.Context.GraphicsApi));
    private readonly ComputePipeline composePipeline = CreatePipeline(vendor.Context, UpscalingBridge.RayReconstructionComposeCS(vendor.Context.GraphicsApi));
    private readonly Texture motionVectors = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture normals = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture diffuseInput = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture diffuseOutput = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture specularInput = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture specularOutput = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture residual = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture composedColor = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture transparencyMask = CreateTexture(vendor.Context, PixelFormat.R8UNorm, desc.RenderWidth, desc.RenderHeight);

    private Texture previousLinearDepth = CreateTexture(vendor.Context, PixelFormat.R32Float, desc.RenderWidth, desc.RenderHeight);
    private Texture linearDepth = CreateTexture(vendor.Context, PixelFormat.R32Float, desc.RenderWidth, desc.RenderHeight);
    private FSRContext denoiserContext;
    private FSRContext upscalerContext;
    private Vector3 previousCameraPosition;
    private uint frameIndex;

    protected override void Build(RayReconstructionArgs args)
    {
        vendor.DestroyFeature(upscalerContext);
        vendor.DestroyFeature(denoiserContext);

        denoiserContext = vendor.CreateFeature(Desc);
        upscalerContext = vendor.CreateFeature(new SuperResolutionDesc
        {
            Format = Desc.Format,
            RenderWidth = Desc.RenderWidth,
            RenderHeight = Desc.RenderHeight,
            OutputWidth = Desc.OutputWidth,
            OutputHeight = Desc.OutputHeight
        }, new()
        {
            Color = composedColor,
            Depth = args.Depth,
            MotionVectors = args.MotionVectors,
            ReactiveMask = args.ReactiveMask,
            Exposure = args.Exposure,
            Projection = args.Projection
        });
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        if (args.Reset)
        {
            frameIndex = 0;
        }

        UpscalingHelper.DecomposePerspective(args.Projection, out float nearPlane, out float farPlane, out _, out _, out _, out bool depthInfinite);

        PrepareConstants prepareConstants = new()
        {
            RenderWidth = Desc.RenderWidth,
            RenderHeight = Desc.RenderHeight,
            HasSpecularHitDistance = args.SpecularHitDistance is not null ? 1u : 0u,
            Reset = args.Reset ? 1u : 0u,
            M33 = args.Projection.M33,
            M34 = args.Projection.M34,
            M43 = args.Projection.M43,
            MinLinearDepth = nearPlane,
            MaxLinearDepth = depthInfinite ? 1e6f : farPlane,
            Color = args.Color.SampledHandle,
            Depth = args.Depth.SampledHandle,
            MotionVectors = args.MotionVectors.SampledHandle,
            DiffuseAlbedo = args.DiffuseAlbedo.SampledHandle,
            SpecularAlbedo = args.SpecularAlbedo.SampledHandle,
            Normals = args.Normals.SampledHandle,
            Roughness = args.Roughness.SampledHandle,
            SpecularHitDistance = args.SpecularHitDistance?.SampledHandle ?? default,
            PreviousLinearDepth = previousLinearDepth.SampledHandle,
            LinearDepth = linearDepth.StorageHandle,
            MotionVectors3D = motionVectors.StorageHandle,
            PackedNormals = normals.StorageHandle,
            DiffuseRadiance = diffuseInput.StorageHandle,
            SpecularRadiance = specularInput.StorageHandle,
            Residual = residual.StorageHandle
        };

        commandBuffer.Upload(prepareBuffer, 0, new()
        {
            Pointer = (nint)(&prepareConstants),
            SizeInBytes = (uint)sizeof(PrepareConstants)
        });
        commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.ComputeShading);

        FSRVendor.Transition(commandBuffer, [linearDepth, motionVectors, normals, diffuseInput, specularInput, residual], TextureLayout.Undefined, TextureLayout.Storage);

        ThreadGroupSize prepareGroup = preparePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(preparePipeline);
        commandBuffer.SetConstantBuffer(prepareBuffer, 0);
        commandBuffer.Dispatch((Desc.RenderWidth + prepareGroup.X - 1) / prepareGroup.X, (Desc.RenderHeight + prepareGroup.Y - 1) / prepareGroup.Y, 1);

        FSRVendor.Transition(commandBuffer, [linearDepth, motionVectors, normals, diffuseInput, specularInput], TextureLayout.Storage, TextureLayout.Common);
        FSRVendor.Transition(commandBuffer, [residual], TextureLayout.Storage, TextureLayout.Sampled);
        FSRVendor.Transition(commandBuffer, [args.SpecularAlbedo, args.DiffuseAlbedo], TextureLayout.Sampled, TextureLayout.Common);
        FSRVendor.Transition(commandBuffer, [diffuseOutput, specularOutput], TextureLayout.Undefined, TextureLayout.Common);

        Matrix4x4.Invert(args.View, out Matrix4x4 viewToWorld);
        Vector3 cameraPosition = viewToWorld.Translation;
        Vector3 cameraPositionDelta = args.Reset ? Vector3.Zero : previousCameraPosition - cameraPosition;

        commandBuffer.BeginExternalCommands();

        vendor.EvaluateFeature(commandBuffer, denoiserContext, args, linearDepth, motionVectors, normals, diffuseInput, diffuseOutput, specularInput, specularOutput, cameraPositionDelta, frameIndex);

        commandBuffer.EndExternalCommands();

        FSRVendor.Transition(commandBuffer, [linearDepth, args.SpecularAlbedo, args.DiffuseAlbedo, diffuseOutput, specularOutput], TextureLayout.Common, TextureLayout.Sampled);

        ComposeConstants composeConstants = new()
        {
            RenderWidth = Desc.RenderWidth,
            RenderHeight = Desc.RenderHeight,
            HasTransparencyOverlay = args.TransparencyOverlay is not null ? 1u : 0u,
            DiffuseDenoised = diffuseOutput.SampledHandle,
            SpecularDenoised = specularOutput.SampledHandle,
            DiffuseAlbedo = args.DiffuseAlbedo.SampledHandle,
            SpecularAlbedo = args.SpecularAlbedo.SampledHandle,
            Residual = residual.SampledHandle,
            TransparencyOverlay = args.TransparencyOverlay?.SampledHandle ?? default,
            ComposedColor = composedColor.StorageHandle,
            TransparencyMask = transparencyMask.StorageHandle
        };

        commandBuffer.Upload(composeBuffer, 0, new()
        {
            Pointer = (nint)(&composeConstants),
            SizeInBytes = (uint)sizeof(ComposeConstants)
        });
        commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.ComputeShading);

        Texture? activeTransparencyMask = args.TransparencyOverlay is not null ? transparencyMask : null;

        FSRVendor.Transition(commandBuffer, [composedColor, activeTransparencyMask], TextureLayout.Undefined, TextureLayout.Storage);

        ThreadGroupSize composeGroup = composePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(composePipeline);
        commandBuffer.SetConstantBuffer(composeBuffer, 0);
        commandBuffer.Dispatch((Desc.RenderWidth + composeGroup.X - 1) / composeGroup.X, (Desc.RenderHeight + composeGroup.Y - 1) / composeGroup.Y, 1);

        FSRVendor.Transition(commandBuffer, [composedColor, activeTransparencyMask], TextureLayout.Storage, TextureLayout.Common);
        FSRVendor.Transition(commandBuffer, [args.Depth, args.MotionVectors, args.ReactiveMask, args.Exposure], TextureLayout.Sampled, TextureLayout.Common);
        FSRVendor.Transition(commandBuffer, [Output], args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Common);

        commandBuffer.BeginExternalCommands();

        vendor.EvaluateFeature(commandBuffer, upscalerContext, new()
        {
            Color = composedColor,
            Depth = args.Depth,
            MotionVectors = args.MotionVectors,
            ReactiveMask = args.ReactiveMask,
            Exposure = args.Exposure,
            JitterOffset = args.JitterOffset,
            Projection = args.Projection,
            DeltaTimeInSeconds = args.DeltaTimeInSeconds,
            PreExposure = args.PreExposure,
            Reset = args.Reset
        }, activeTransparencyMask, Output);

        commandBuffer.EndExternalCommands();

        FSRVendor.Transition(commandBuffer, [args.Depth, args.MotionVectors, args.ReactiveMask, args.Exposure, Output], TextureLayout.Common, TextureLayout.Sampled);

        (previousLinearDepth, linearDepth) = (linearDepth, previousLinearDepth);
        previousCameraPosition = cameraPosition;
        frameIndex++;
    }

    protected override void Destroy()
    {
        vendor.DestroyFeature(upscalerContext);
        vendor.DestroyFeature(denoiserContext);

        linearDepth.Dispose();
        previousLinearDepth.Dispose();
        transparencyMask.Dispose();
        composedColor.Dispose();
        residual.Dispose();
        specularOutput.Dispose();
        specularInput.Dispose();
        diffuseOutput.Dispose();
        diffuseInput.Dispose();
        normals.Dispose();
        motionVectors.Dispose();
        composePipeline.Dispose();
        preparePipeline.Dispose();
        composeBuffer.Dispose();
        prepareBuffer.Dispose();

        base.Destroy();
    }

    private static ComputePipeline CreatePipeline(GraphicsContext context, ShaderDesc desc)
    {
        using Shader shader = context.CreateShader(desc);

        return context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    private static Texture CreateTexture(GraphicsContext context, PixelFormat format, uint width, uint height)
    {
        return context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
    }
}

[StructLayout(LayoutKind.Explicit, Size = 160)]
file struct PrepareConstants
{
    [FieldOffset(0)]
    public uint RenderWidth;

    [FieldOffset(4)]
    public uint RenderHeight;

    [FieldOffset(8)]
    public uint HasSpecularHitDistance;

    [FieldOffset(12)]
    public uint Reset;

    [FieldOffset(16)]
    public float M33;

    [FieldOffset(20)]
    public float M34;

    [FieldOffset(24)]
    public float M43;

    [FieldOffset(28)]
    public float MinLinearDepth;

    [FieldOffset(32)]
    public float MaxLinearDepth;

    [FieldOffset(40)]
    public ResourceHandle Color;

    [FieldOffset(48)]
    public ResourceHandle Depth;

    [FieldOffset(56)]
    public ResourceHandle MotionVectors;

    [FieldOffset(64)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(72)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(80)]
    public ResourceHandle Normals;

    [FieldOffset(88)]
    public ResourceHandle Roughness;

    [FieldOffset(96)]
    public ResourceHandle SpecularHitDistance;

    [FieldOffset(104)]
    public ResourceHandle PreviousLinearDepth;

    [FieldOffset(112)]
    public ResourceHandle LinearDepth;

    [FieldOffset(120)]
    public ResourceHandle MotionVectors3D;

    [FieldOffset(128)]
    public ResourceHandle PackedNormals;

    [FieldOffset(136)]
    public ResourceHandle DiffuseRadiance;

    [FieldOffset(144)]
    public ResourceHandle SpecularRadiance;

    [FieldOffset(152)]
    public ResourceHandle Residual;
}

[StructLayout(LayoutKind.Explicit, Size = 80)]
file struct ComposeConstants
{
    [FieldOffset(0)]
    public uint RenderWidth;

    [FieldOffset(4)]
    public uint RenderHeight;

    [FieldOffset(8)]
    public uint HasTransparencyOverlay;

    [FieldOffset(16)]
    public ResourceHandle DiffuseDenoised;

    [FieldOffset(24)]
    public ResourceHandle SpecularDenoised;

    [FieldOffset(32)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(40)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(48)]
    public ResourceHandle Residual;

    [FieldOffset(56)]
    public ResourceHandle TransparencyOverlay;

    [FieldOffset(64)]
    public ResourceHandle ComposedColor;

    [FieldOffset(72)]
    public ResourceHandle TransparencyMask;
}
