using System.Numerics;
using System.Runtime.InteropServices;
using Sponza.Helpers;
using Sponza.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace Sponza.Passes;

internal class EnvironmentPass : IDisposable
{
    private readonly ComputePipeline pipeline;
    private readonly Texture environment;
    private readonly TextureView[] mipViews;
    private readonly Buffer[] constantBuffers;

    private bool dirty = true;
    private bool initialized;

    public EnvironmentPass(GraphicsContext context)
    {
        pipeline = GraphicsHelper.CreateComputePipeline(context, "Environment.slang", "CSMain");
        environment = context.CreateTexture(TextureDesc.TextureCube(PixelFormat.R16G16B16A16Float, 128, 8) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });

        mipViews = new TextureView[environment.Desc.MipLevels];
        constantBuffers = new Buffer[environment.Desc.MipLevels];
        for (uint mip = 0; mip < environment.Desc.MipLevels; mip++)
        {
            mipViews[mip] = context.CreateTextureView(TextureViewDesc.Texture2DArray(environment, environment.Desc.Format, 0, environment.Desc.ArrayLayers, mip, 1));
            constantBuffers[mip] = GraphicsHelper.CreateConstantBuffer<EnvironmentConstants>(context);
        }
    }

    public void Invalidate()
    {
        dirty = true;
    }

    public EnvironmentData Record(CommandBuffer commandBuffer, SkyData sky)
    {
        if (!dirty)
        {
            return new() { PrefilteredEnvironment = environment };
        }

        TextureLayout previousLayout = initialized ? TextureLayout.Sampled : TextureLayout.Undefined;
        commandBuffer.SetPipeline(pipeline);

        for (uint mip = 0; mip < environment.Desc.MipLevels; mip++)
        {
            ZenithHelper.MipDimensions(environment.Desc.Width, environment.Desc.Height, environment.Desc.Depth, mip, out uint width, out uint height, out _);

            EnvironmentConstants constants = new()
            {
                SunDirectionAndIntensity = new(sky.SunDirectionWorld, sky.SkyIntensity),
                SunRadiance = new(sky.SunRadiance, 0.0f),
                ZenithColor = new(sky.ZenithColor, 0.0f),
                HorizonColor = new(sky.HorizonColor, 0.0f),
                GroundColor = new(sky.GroundColor, 0.0f),
                Output = mipViews[mip].StorageHandle,
                Size = width,
                Roughness = (float)mip / (environment.Desc.MipLevels - 1)
            };
            GraphicsHelper.Upload(constantBuffers[mip], constants);

            for (uint face = 0; face < environment.Desc.ArrayLayers; face++)
            {
                commandBuffer.Transition(environment, new()
                {
                    MipLevel = mip,
                    ArrayLayer = face
                }, previousLayout, TextureLayout.Storage);
            }

            commandBuffer.SetConstantBuffer(constantBuffers[mip], 0);
            commandBuffer.Dispatch((width + 7) / 8, (height + 7) / 8, environment.Desc.ArrayLayers);

            for (uint face = 0; face < environment.Desc.ArrayLayers; face++)
            {
                commandBuffer.Transition(environment, new()
                {
                    MipLevel = mip,
                    ArrayLayer = face
                }, TextureLayout.Storage, TextureLayout.Sampled);
            }
        }

        dirty = false;
        initialized = true;

        return new() { PrefilteredEnvironment = environment };
    }

    public void Dispose()
    {
        for (int mip = constantBuffers.Length - 1; mip >= 0; mip--)
        {
            constantBuffers[mip].Dispose();
            mipViews[mip].Dispose();
        }

        environment.Dispose();
        pipeline.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 96)]
file struct EnvironmentConstants
{
    [FieldOffset(0)]
    public Vector4 SunDirectionAndIntensity;

    [FieldOffset(16)]
    public Vector4 SunRadiance;

    [FieldOffset(32)]
    public Vector4 ZenithColor;

    [FieldOffset(48)]
    public Vector4 HorizonColor;

    [FieldOffset(64)]
    public Vector4 GroundColor;

    [FieldOffset(80)]
    public ResourceHandle Output;

    [FieldOffset(88)]
    public uint Size;

    [FieldOffset(92)]
    public float Roughness;
}
