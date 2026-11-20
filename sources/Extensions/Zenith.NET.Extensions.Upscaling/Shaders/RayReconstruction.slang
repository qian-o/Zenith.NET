struct PrepareConstants
{
    uint RenderWidth;

    uint RenderHeight;

    uint HasSpecularHitDistance;

    uint Reset;

    float M33;

    float M34;

    float M43;

    float MinLinearDepth;

    float MaxLinearDepth;

    uint Padding;

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

    DescriptorHandle<RWTexture2D<float4>> MotionVectors3D;

    DescriptorHandle<RWTexture2D<float4>> PackedNormals;

    DescriptorHandle<RWTexture2D<float4>> DiffuseRadiance;

    DescriptorHandle<RWTexture2D<float4>> SpecularRadiance;
};

struct ComposeConstants
{
    uint RenderWidth;

    uint RenderHeight;

    uint HasTransparencyOverlay;

    uint Padding;

    DescriptorHandle<Texture2D<float4>> Color;

    DescriptorHandle<Texture2D<float4>> DiffuseDenoised;

    DescriptorHandle<Texture2D<float4>> SpecularDenoised;

    DescriptorHandle<Texture2D<float4>> DiffuseAlbedo;

    DescriptorHandle<Texture2D<float4>> SpecularAlbedo;

    DescriptorHandle<Texture2D<float4>> TransparencyOverlay;

    DescriptorHandle<RWTexture2D<float4>> ComposedColor;

    DescriptorHandle<RWTexture2D<float>> TransparencyMask;
};

#if 0
ConstantBuffer<PrepareConstants> prepare;
#else
ConstantBuffer<ComposeConstants> compose;
#endif

float3 Sanitize(float3 value)
{
    return select(isfinite(value), max(value, 0.0), 0.0);
}

float3 Demodulate(float3 color, float3 diffuseAlbedo, float3 specularAlbedo, out float3 diffuseRadiance,
                  out float3 specularRadiance)
{
    float3 totalAlbedo = diffuseAlbedo + specularAlbedo;
    float3 modulatedMask = saturate((totalAlbedo - 0.005) / 0.015);
    float3 safeTotal = max(totalAlbedo, 1e-4);
    float3 demodulated = min(color * modulatedMask / safeTotal, float3(64.0, 64.0, 64.0));

    diffuseRadiance = select(diffuseAlbedo > 1e-4, demodulated, 0.0);
    specularRadiance = select(specularAlbedo > 1e-4, demodulated, 0.0);

    return max(color - (diffuseRadiance * diffuseAlbedo + specularRadiance * specularAlbedo), 0.0);
}

float2 OctahedralEncode(float3 normal)
{
    float len = length(normal);

    if (!isfinite(len) || len < 1e-6)
    {
        return float2(0.5, 0.5);
    }

    normal /= len;
    normal /= abs(normal.x) + abs(normal.y) + abs(normal.z);

    if (normal.z < 0.0)
    {
        float2 signNotZero = select(normal.xy >= 0.0, 1.0, -1.0);
        normal.xy = (1.0 - abs(normal.yx)) * signNotZero;
    }

    return saturate(normal.xy * 0.5 + 0.5);
}

#if 0
float ReconstructLinearDepth(float deviceDepth)
{
    float denom = deviceDepth * prepare.M34 - prepare.M33;
    float linearDepth = abs(denom) > 1e-6 ? abs(prepare.M43 / denom) : prepare.MaxLinearDepth;

    if (!isfinite(linearDepth))
    {
        return prepare.MaxLinearDepth;
    }

    return clamp(linearDepth, prepare.MinLinearDepth, prepare.MaxLinearDepth);
}

float SamplePreviousLinearDepth(float2 previousUv, float currentLinearDepth)
{
    float2 renderSize = float2(prepare.RenderWidth, prepare.RenderHeight);
    float2 texelCoord = previousUv * renderSize - 0.5;
    int2 basePixel = int2(floor(texelCoord));
    float2 fracCoord = frac(texelCoord);
    int2 maxPixel = int2(prepare.RenderWidth - 1, prepare.RenderHeight - 1);

    int2 p00 = clamp(basePixel + int2(0, 0), int2(0, 0), maxPixel);
    int2 p10 = clamp(basePixel + int2(1, 0), int2(0, 0), maxPixel);
    int2 p01 = clamp(basePixel + int2(0, 1), int2(0, 0), maxPixel);
    int2 p11 = clamp(basePixel + int2(1, 1), int2(0, 0), maxPixel);

    float d00 = prepare.PreviousLinearDepth.Load(int3(p00, 0));
    float d10 = prepare.PreviousLinearDepth.Load(int3(p10, 0));
    float d01 = prepare.PreviousLinearDepth.Load(int3(p01, 0));
    float d11 = prepare.PreviousLinearDepth.Load(int3(p11, 0));

    float depthScale = max(currentLinearDepth * 0.05, 0.05);

    float w00 = (1.0 - fracCoord.x) * (1.0 - fracCoord.y) * exp(-abs(d00 - currentLinearDepth) / depthScale);
    float w10 = fracCoord.x * (1.0 - fracCoord.y) * exp(-abs(d10 - currentLinearDepth) / depthScale);
    float w01 = (1.0 - fracCoord.x) * fracCoord.y * exp(-abs(d01 - currentLinearDepth) / depthScale);
    float w11 = fracCoord.x * fracCoord.y * exp(-abs(d11 - currentLinearDepth) / depthScale);

    float weightSum = w00 + w10 + w01 + w11;

    if (weightSum > 1e-5)
    {
        return (d00 * w00 + d10 * w10 + d01 * w01 + d11 * w11) / weightSum;
    }

    int2 nearest = clamp(int2(previousUv * renderSize), int2(0, 0), maxPixel);

    return prepare.PreviousLinearDepth.Load(int3(nearest, 0));
}

[shader("compute")]
[numthreads(8, 8, 1)]
void PrepareCS(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;

    if (pixel.x >= prepare.RenderWidth || pixel.y >= prepare.RenderHeight)
    {
        return;
    }

    float3 color = Sanitize(prepare.Color.Load(int3(pixel, 0)).rgb);
    float deviceDepth = prepare.Depth.Load(int3(pixel, 0));
    float2 motionVector = prepare.MotionVectors.Load(int3(pixel, 0));
    float3 diffuseAlbedo = Sanitize(prepare.DiffuseAlbedo.Load(int3(pixel, 0)).rgb);
    float3 specularAlbedo = Sanitize(prepare.SpecularAlbedo.Load(int3(pixel, 0)).rgb);
    float3 normal = prepare.Normals.Load(int3(pixel, 0)).rgb;
    float roughness = saturate(prepare.Roughness.Load(int3(pixel, 0)));

    float linearDepth = ReconstructLinearDepth(deviceDepth);
    float linearDepthDelta = 0.0;

    if (prepare.Reset == 0)
    {
        float2 currentUv = (float2(pixel) + 0.5) / float2(prepare.RenderWidth, prepare.RenderHeight);
        float2 previousUv = currentUv + motionVector;

        if (all(previousUv >= 0.0) && all(previousUv <= 1.0))
        {
            float previousLinearDepth = SamplePreviousLinearDepth(previousUv, linearDepth);

            if (isfinite(previousLinearDepth))
            {
                linearDepthDelta = previousLinearDepth - linearDepth;
            }
        }
    }

    float3 diffuseRadiance;
    float3 specularRadiance;
    Demodulate(color, diffuseAlbedo, specularAlbedo, diffuseRadiance, specularRadiance);

    float diffuseHitDistance = max(linearDepth * 0.5, 0.1);
    float specularHitDistance = prepare.HasSpecularHitDistance != 0
                                    ? max(prepare.SpecularHitDistance.Load(int3(pixel, 0)), 0.0)
                                    : max(linearDepth * lerp(1.0, 0.25, roughness), 0.1);

    if (!isfinite(specularHitDistance))
    {
        specularHitDistance = prepare.MaxLinearDepth;
    }

    prepare.LinearDepth[pixel] = linearDepth;
    prepare.MotionVectors3D[pixel] = float4(motionVector, linearDepthDelta, 0.0);
    prepare.PackedNormals[pixel] = float4(OctahedralEncode(normal), roughness, 0.0);
    prepare.DiffuseRadiance[pixel] = float4(diffuseRadiance, diffuseHitDistance);
    prepare.SpecularRadiance[pixel] = float4(specularRadiance, specularHitDistance);
}
#else
[shader("compute")]
[numthreads(8, 8, 1)]
void ComposeCS(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;

    if (pixel.x >= compose.RenderWidth || pixel.y >= compose.RenderHeight)
    {
        return;
    }

    float3 sourceColor = Sanitize(compose.Color.Load(int3(pixel, 0)).rgb);
    float3 diffuse = Sanitize(compose.DiffuseDenoised.Load(int3(pixel, 0)).rgb);
    float3 specular = Sanitize(compose.SpecularDenoised.Load(int3(pixel, 0)).rgb);
    float3 diffuseAlbedo = Sanitize(compose.DiffuseAlbedo.Load(int3(pixel, 0)).rgb);
    float3 specularAlbedo = Sanitize(compose.SpecularAlbedo.Load(int3(pixel, 0)).rgb);

    float3 diffuseRadiance;
    float3 specularRadiance;
    float3 residual = Demodulate(sourceColor, diffuseAlbedo, specularAlbedo, diffuseRadiance, specularRadiance);

    float3 color = diffuse * diffuseAlbedo + specular * specularAlbedo + residual;

    if (compose.HasTransparencyOverlay != 0)
    {
        float4 overlay = compose.TransparencyOverlay.Load(int3(pixel, 0));
        float alpha = isfinite(overlay.a) ? saturate(overlay.a) : 0.0;

        color = lerp(color, Sanitize(overlay.rgb), alpha);
        compose.TransparencyMask[pixel] = alpha;
    }

    compose.ComposedColor[pixel] = float4(color, 1.0);
}
#endif
