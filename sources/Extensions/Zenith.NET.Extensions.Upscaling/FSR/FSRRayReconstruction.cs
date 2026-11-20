using System.Numerics;
using System.Runtime.InteropServices;
using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal unsafe class FSRRayReconstruction(FSRVendor vendor, RayReconstructionDesc desc) : RayReconstruction(vendor.Context, desc)
{
    private readonly Buffer constants = vendor.Context.CreateBuffer(BufferDesc.Constant((uint)sizeof(PrepareConstants)));
    private readonly Texture motionVectors = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture normals = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture diffuseInput = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture diffuseOutput = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture specularInput = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);
    private readonly Texture specularOutput = CreateTexture(vendor.Context, PixelFormat.R16G16B16A16Float, desc.RenderWidth, desc.RenderHeight);

    private Texture previousLinearDepth = CreateTexture(vendor.Context, PixelFormat.R32Float, desc.RenderWidth, desc.RenderHeight);
    private Texture linearDepth = CreateTexture(vendor.Context, PixelFormat.R32Float, desc.RenderWidth, desc.RenderHeight);
    private Texture? transparencyMask;
    private FSRContext denoiser;
    private FSRContext upscaler;
    private Vector3 previousCameraPosition;
    private uint frameIndex;

    protected override void Build(RayReconstructionArgs args)
    {
        vendor.DestroyFeature(upscaler);
        vendor.DestroyFeature(denoiser);

        vendor.CreateFeature(Desc, args, out denoiser, out upscaler);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        if (args.Reset)
        {
            frameIndex = 0;
        }

        (float nearPlane, float farPlane, _, _, _, bool depthInfinite) = UpscalingHelper.DecomposePerspective(args.Projection);

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
            SpecularRadiance = specularInput.StorageHandle
        };

        commandBuffer.Upload(constants, 0, new()
        {
            Pointer = (nint)(&prepareConstants),
            SizeInBytes = (uint)sizeof(PrepareConstants)
        });
        commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.ComputeShading);

        FSRVendor.Transition(commandBuffer, [linearDepth, motionVectors, normals, diffuseInput, specularInput], TextureLayout.Undefined, TextureLayout.Storage);

        ThreadGroupSize prepareGroup = vendor.PreparePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(vendor.PreparePipeline);
        commandBuffer.SetConstantBuffer(constants, 0);
        commandBuffer.Dispatch((Desc.RenderWidth + prepareGroup.X - 1) / prepareGroup.X, (Desc.RenderHeight + prepareGroup.Y - 1) / prepareGroup.Y, 1);

        FSRVendor.Transition(commandBuffer, [linearDepth, motionVectors, normals, diffuseInput, specularInput], TextureLayout.Storage, TextureLayout.Common);
        FSRVendor.Transition(commandBuffer, [args.SpecularAlbedo, args.DiffuseAlbedo], TextureLayout.Sampled, TextureLayout.Common);
        FSRVendor.Transition(commandBuffer, [diffuseOutput, specularOutput], TextureLayout.Undefined, TextureLayout.Common);

        Vector3 cameraPosition = UpscalingHelper.DecomposeCamera(args.View, args.Projection).Position;
        Vector3 cameraPositionDelta = args.Reset ? Vector3.Zero : previousCameraPosition - cameraPosition;

        commandBuffer.BeginExternalCommands();

        vendor.EvaluateFeature(commandBuffer, denoiser, args, linearDepth, motionVectors, normals, diffuseInput, diffuseOutput, specularInput, specularOutput, cameraPositionDelta, frameIndex);

        commandBuffer.EndExternalCommands();

        FSRVendor.Transition(commandBuffer, [linearDepth, args.SpecularAlbedo, args.DiffuseAlbedo, diffuseOutput, specularOutput], TextureLayout.Common, TextureLayout.Sampled);

        Texture? activeTransparencyMask = args.TransparencyOverlay is not null ? transparencyMask ??= CreateTexture(Context, PixelFormat.R8UNorm, Desc.RenderWidth, Desc.RenderHeight) : null;

        ComposeConstants composeConstants = new()
        {
            RenderWidth = Desc.RenderWidth,
            RenderHeight = Desc.RenderHeight,
            HasTransparencyOverlay = activeTransparencyMask is not null ? 1u : 0u,
            Color = args.Color.SampledHandle,
            DiffuseDenoised = diffuseOutput.SampledHandle,
            SpecularDenoised = specularOutput.SampledHandle,
            DiffuseAlbedo = args.DiffuseAlbedo.SampledHandle,
            SpecularAlbedo = args.SpecularAlbedo.SampledHandle,
            TransparencyOverlay = args.TransparencyOverlay?.SampledHandle ?? default,
            ComposedColor = diffuseInput.StorageHandle,
            TransparencyMask = activeTransparencyMask?.StorageHandle ?? default
        };

        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.Copy);
        commandBuffer.Upload(constants, 0, new()
        {
            Pointer = (nint)(&composeConstants),
            SizeInBytes = (uint)sizeof(ComposeConstants)
        });
        commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.ComputeShading);

        FSRVendor.Transition(commandBuffer, [diffuseInput], TextureLayout.Common, TextureLayout.Storage);
        FSRVendor.Transition(commandBuffer, [activeTransparencyMask], TextureLayout.Undefined, TextureLayout.Storage);

        ThreadGroupSize composeGroup = vendor.ComposePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(vendor.ComposePipeline);
        commandBuffer.SetConstantBuffer(constants, 0);
        commandBuffer.Dispatch((Desc.RenderWidth + composeGroup.X - 1) / composeGroup.X, (Desc.RenderHeight + composeGroup.Y - 1) / composeGroup.Y, 1);

        FSRVendor.Transition(commandBuffer, [diffuseInput, activeTransparencyMask], TextureLayout.Storage, TextureLayout.Common);
        FSRVendor.Transition(commandBuffer, [args.Depth, args.MotionVectors, args.ReactiveMask, args.Exposure], TextureLayout.Sampled, TextureLayout.Common);
        FSRVendor.Transition(commandBuffer, [Output], args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Common);

        commandBuffer.BeginExternalCommands();

        vendor.EvaluateFeature(commandBuffer, upscaler, args, diffuseInput, activeTransparencyMask, Output);

        commandBuffer.EndExternalCommands();

        FSRVendor.Transition(commandBuffer, [args.Depth, args.MotionVectors, args.ReactiveMask, args.Exposure, Output], TextureLayout.Common, TextureLayout.Sampled);

        (previousLinearDepth, linearDepth) = (linearDepth, previousLinearDepth);
        previousCameraPosition = cameraPosition;
        frameIndex++;
    }

    protected override void Destroy()
    {
        vendor.DestroyFeature(upscaler);
        vendor.DestroyFeature(denoiser);

        transparencyMask?.Dispose();
        linearDepth.Dispose();
        previousLinearDepth.Dispose();
        specularOutput.Dispose();
        specularInput.Dispose();
        diffuseOutput.Dispose();
        diffuseInput.Dispose();
        normals.Dispose();
        motionVectors.Dispose();
        constants.Dispose();

        base.Destroy();
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
    public ResourceHandle Color;

    [FieldOffset(24)]
    public ResourceHandle DiffuseDenoised;

    [FieldOffset(32)]
    public ResourceHandle SpecularDenoised;

    [FieldOffset(40)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(48)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(56)]
    public ResourceHandle TransparencyOverlay;

    [FieldOffset(64)]
    public ResourceHandle ComposedColor;

    [FieldOffset(72)]
    public ResourceHandle TransparencyMask;
}
