struct PreparationConstants
{
    uint Width;

    uint Height;

    uint SignalFlags;

    uint Padding;

    float4 ProjectionDepth;

    DescriptorHandle<Texture2D<float>> Depth;

    DescriptorHandle<Texture2D<float2>> MotionVectors;

    DescriptorHandle<Texture2D<float4>> Normals;

    DescriptorHandle<Texture2D<float>> Roughness;

    DescriptorHandle<Texture2D<float>> LinearDepthDelta;

    DescriptorHandle<RWTexture2D<float>> LinearDepth;

    DescriptorHandle<RWTexture2D<float4>> PackedMotion;

    DescriptorHandle<RWTexture2D<float4>> NormalRoughness;
};

struct CompositionConstants
{
    uint Width;

    uint Height;

    uint SignalFlags;

    uint HasTransparencyOverlay;

    DescriptorHandle<Texture2D<float4>> Color;

    DescriptorHandle<Texture2D<float4>> DiffuseAlbedo;

    DescriptorHandle<Texture2D<float4>> SpecularAlbedo;

    DescriptorHandle<Texture2D<float4>> TransparencyOverlay;

    DescriptorHandle<Texture2D<float4>> DirectDiffuse;

    DescriptorHandle<Texture2D<float4>> DirectSpecular;

    DescriptorHandle<Texture2D<float4>> IndirectDiffuse;

    DescriptorHandle<Texture2D<float4>> IndirectSpecular;

    DescriptorHandle<Texture2D<float4>> DenoisedDirectDiffuse;

    DescriptorHandle<Texture2D<float4>> DenoisedDirectSpecular;

    DescriptorHandle<Texture2D<float4>> DenoisedIndirectDiffuse;

    DescriptorHandle<Texture2D<float4>> DenoisedIndirectSpecular;

    DescriptorHandle<RWTexture2D<float4>> CompositedColor;

    DescriptorHandle<RWTexture2D<float>> TransparencyMask;
};

ConstantBuffer<PreparationConstants> preparation;
ConstantBuffer<CompositionConstants> composition;

float2 EncodeNormal(float3 normal)
{
    normal /= max(abs(normal.x) + abs(normal.y) + abs(normal.z), 1e-6);
    float2 encoded = normal.xy;

    if (normal.z < 0.0)
    {
        float2 hemisphere = float2(normal.x >= 0.0 ? 1.0 : -1.0, normal.y >= 0.0 ? 1.0 : -1.0);
        encoded = (1.0 - abs(encoded.yx)) * hemisphere;
    }

    return encoded * 0.5 + 0.5;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Prepare(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;

    if (pixel.x >= preparation.Width || pixel.y >= preparation.Height)
    {
        return;
    }

    float depth = preparation.Depth[pixel];
    float4 projection = preparation.ProjectionDepth;

    preparation.LinearDepth[pixel] = (projection.z - depth * projection.w) / (depth * projection.y - projection.x);
    preparation.PackedMotion[pixel] = float4(preparation.MotionVectors[pixel], preparation.LinearDepthDelta[pixel], 0.0);
    preparation.NormalRoughness[pixel] = float4(EncodeNormal(preparation.Normals[pixel].xyz), preparation.Roughness[pixel], 0.0);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Compose(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;

    if (pixel.x >= composition.Width || pixel.y >= composition.Height)
    {
        return;
    }

    float4 color = composition.Color[pixel];
    float3 diffuseAlbedo = composition.DiffuseAlbedo[pixel].rgb;
    float3 specularAlbedo = composition.SpecularAlbedo[pixel].rgb;
    float3 difference = 0.0;

    if ((composition.SignalFlags & 2) != 0)
    {
        difference += (composition.DenoisedDirectDiffuse[pixel].rgb - composition.DirectDiffuse[pixel].rgb) * diffuseAlbedo;
    }

    if ((composition.SignalFlags & 4) != 0)
    {
        difference += (composition.DenoisedDirectSpecular[pixel].rgb - composition.DirectSpecular[pixel].rgb) * specularAlbedo;
    }

    if ((composition.SignalFlags & 16) != 0)
    {
        difference += (composition.DenoisedIndirectDiffuse[pixel].rgb - composition.IndirectDiffuse[pixel].rgb) * diffuseAlbedo;
    }

    if ((composition.SignalFlags & 32) != 0)
    {
        difference += (composition.DenoisedIndirectSpecular[pixel].rgb - composition.IndirectSpecular[pixel].rgb) * specularAlbedo;
    }

    color.rgb += difference;

    if (composition.HasTransparencyOverlay != 0)
    {
        composition.TransparencyMask[pixel] = composition.TransparencyOverlay[pixel].a;
    }

    composition.CompositedColor[pixel] = color;
}
