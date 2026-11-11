struct PreparationConstants
{
    uint Width;

    uint Height;

    uint HasHistory;

    uint Padding;

    float4 ProjectionDepth;

    DescriptorHandle<Texture2D<float4>> Color;

    DescriptorHandle<Texture2D<float>> Depth;

    DescriptorHandle<Texture2D<float2>> MotionVectors;

    DescriptorHandle<Texture2D<float4>> DiffuseAlbedo;

    DescriptorHandle<Texture2D<float4>> SpecularAlbedo;

    DescriptorHandle<Texture2D<float4>> Normals;

    DescriptorHandle<Texture2D<float>> Roughness;

    DescriptorHandle<Texture2D<float>> SpecularHitDistance;

    DescriptorHandle<Texture2D<float>> PreviousLinearDepth;

    DescriptorHandle<RWTexture2D<float>> LinearDepth;

    DescriptorHandle<RWTexture2D<float4>> PackedMotion;

    DescriptorHandle<RWTexture2D<float4>> NormalRoughness;

    DescriptorHandle<RWTexture2D<float4>> DiffuseRadiance;

    DescriptorHandle<RWTexture2D<float4>> SpecularRadiance;
};

struct CompositionConstants
{
    uint Width;

    uint Height;

    uint HasTransparencyOverlay;

    uint Padding;

    DescriptorHandle<Texture2D<float4>> Color;

    DescriptorHandle<Texture2D<float4>> DiffuseAlbedo;

    DescriptorHandle<Texture2D<float4>> SpecularAlbedo;

    DescriptorHandle<Texture2D<float4>> TransparencyOverlay;

    DescriptorHandle<Texture2D<float4>> DiffuseRadiance;

    DescriptorHandle<Texture2D<float4>> SpecularRadiance;

    DescriptorHandle<Texture2D<float4>> DenoisedDiffuse;

    DescriptorHandle<Texture2D<float4>> DenoisedSpecular;

    DescriptorHandle<RWTexture2D<float4>> CompositedColor;

    DescriptorHandle<RWTexture2D<float>> TransparencyMask;
};

ConstantBuffer<PreparationConstants> preparation;
ConstantBuffer<CompositionConstants> composition;

float Luminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

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
    float linearDepth = abs((projection.z - depth * projection.w) / (depth * projection.y - projection.x));
    float2 motionVector = preparation.MotionVectors[pixel];
    float linearDepthDelta = 0.0;

    if (preparation.HasHistory != 0)
    {
        float2 renderSize = float2(preparation.Width, preparation.Height);
        float2 previousUv = (float2(pixel) + 0.5) / renderSize + motionVector;

        if (all(previousUv >= 0.0) && all(previousUv <= 1.0))
        {
            int2 maxPixel = int2(preparation.Width - 1, preparation.Height - 1);
            int2 previousPixel = clamp(int2(previousUv * renderSize), int2(0), maxPixel);
            float previousDepth = preparation.PreviousLinearDepth.Load(int3(previousPixel, 0));

            linearDepthDelta = previousDepth - linearDepth;
        }
    }

    float3 color = preparation.Color[pixel].rgb;
    float3 diffuseAlbedo = max(preparation.DiffuseAlbedo[pixel].rgb, 0.0);
    float3 specularAlbedo = max(preparation.SpecularAlbedo[pixel].rgb, 0.0);
    float roughness = saturate(preparation.Roughness[pixel]);
    float specularHitDistance = max(preparation.SpecularHitDistance[pixel], 0.0);

    float diffuseWeight = Luminance(diffuseAlbedo);
    float specularWeight = Luminance(specularAlbedo);
    float totalWeight = diffuseWeight + specularWeight;

    float specularRatio = totalWeight > 1e-4 ? (specularWeight / totalWeight) : 0.0;
    float diffuseRatio = totalWeight > 1e-4 ? (1.0 - specularRatio) : 0.0;

    float3 diffuseComponent = color * diffuseRatio;
    float3 specularComponent = color * specularRatio;

    float3 diffuseSignal = select(diffuseAlbedo > 1e-4, diffuseComponent / max(diffuseAlbedo, 1e-4), 0.0);
    float3 specularSignal = select(specularAlbedo > 1e-4, specularComponent / max(specularAlbedo, 1e-4), 0.0);
    float diffuseHitDistance = max(specularHitDistance, linearDepth * (0.1 + 0.9 * roughness));

    preparation.LinearDepth[pixel] = linearDepth;
    preparation.PackedMotion[pixel] = float4(motionVector, linearDepthDelta, 0.0);
    preparation.NormalRoughness[pixel] = float4(EncodeNormal(preparation.Normals[pixel].xyz), roughness, 0.0);
    preparation.DiffuseRadiance[pixel] = float4(diffuseSignal, diffuseHitDistance);
    preparation.SpecularRadiance[pixel] = float4(specularSignal, specularHitDistance);
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
    float3 diffuseAlbedo = max(composition.DiffuseAlbedo[pixel].rgb, 0.0);
    float3 specularAlbedo = max(composition.SpecularAlbedo[pixel].rgb, 0.0);

    float3 difference =
        (composition.DenoisedDiffuse[pixel].rgb - composition.DiffuseRadiance[pixel].rgb) * diffuseAlbedo +
        (composition.DenoisedSpecular[pixel].rgb - composition.SpecularRadiance[pixel].rgb) * specularAlbedo;

    color.rgb = max(color.rgb + difference, 0.0);

    if (composition.HasTransparencyOverlay != 0)
    {
        float4 overlay = composition.TransparencyOverlay[pixel];

        color.rgb = color.rgb * (1.0 - overlay.a) + overlay.rgb;
        composition.TransparencyMask[pixel] = overlay.a;
    }

    composition.CompositedColor[pixel] = color;
}
