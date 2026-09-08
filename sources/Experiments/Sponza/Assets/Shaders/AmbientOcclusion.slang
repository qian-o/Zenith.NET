struct Constants
{
    float4x4 Projection;

    float4x4 InverseProjection;

    DescriptorHandle<Texture2D<float>> DeviceDepth;

    DescriptorHandle<Texture2D<float4>> HdrColor;

    DescriptorHandle<Texture2D<float4>> IndirectDiffuse;

    DescriptorHandle<Texture2D<float>> Occlusion;

    DescriptorHandle<RWTexture2D<float>> OcclusionOutput;

    DescriptorHandle<RWTexture2D<float4>> HdrOutput;

    uint2 RenderSize;

    uint2 HalfSize;

    float RadiusInMeters;

    float Strength;

    float2 Padding;
};

ConstantBuffer<Constants> constants;

float2 PixelUv(int2 pixel)
{
    return (float2(pixel) + 0.5) / float2(constants.RenderSize);
}

int2 FullPixel(float2 uv)
{
    return clamp(int2(uv * float2(constants.RenderSize)), int2(0), int2(constants.RenderSize) - 1);
}

float DeviceDepth(int2 pixel)
{
    return constants.DeviceDepth.Load(int3(clamp(pixel, int2(0), int2(constants.RenderSize) - 1), 0));
}

float3 ViewPosition(float2 uv, float depth)
{
    float4 position = mul(float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), depth, 1.0), constants.InverseProjection);
    return position.xyz / position.w;
}

float3 PixelViewPosition(int2 pixel)
{
    pixel = clamp(pixel, int2(0), int2(constants.RenderSize) - 1);
    return ViewPosition(PixelUv(pixel), DeviceDepth(pixel));
}

float3 GeometryNormal(int2 pixel, float3 position)
{
    float3 left = position - PixelViewPosition(pixel + int2(-1, 0));
    float3 right = PixelViewPosition(pixel + int2(1, 0)) - position;
    float3 up = position - PixelViewPosition(pixel + int2(0, -1));
    float3 down = PixelViewPosition(pixel + int2(0, 1)) - position;
    float3 horizontal = pixel.x == 0 ? right : pixel.x == int(constants.RenderSize.x) - 1 ? left : abs(left.z) < abs(right.z) ? left : right;
    float3 vertical = pixel.y == 0 ? down : pixel.y == int(constants.RenderSize.y) - 1 ? up : abs(up.z) < abs(down.z) ? up : down;
    float3 normal = cross(vertical, horizontal);
    normal = dot(normal, normal) > 1e-12 ? normalize(normal) : float3(0.0, 0.0, 1.0);
    return dot(normal, -position) < 0.0 ? -normal : normal;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Solve(uint3 id : SV_DispatchThreadID)
{
    if (any(id.xy >= constants.HalfSize))
    {
        return;
    }

    int2 pixel = FullPixel((float2(id.xy) + 0.5) / float2(constants.HalfSize));
    float depth = DeviceDepth(pixel);
    if (depth >= 0.999999)
    {
        constants.OcclusionOutput[id.xy] = 1.0;
        return;
    }

    float3 position = ViewPosition(PixelUv(pixel), depth);
    float3 normal = GeometryNormal(pixel, position);
    float3 reference = abs(normal.z) < 0.9 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
    float3 tangent = normalize(cross(reference, normal));
    float3 bitangent = cross(normal, tangent);
    float angle = 6.2831853 * frac(sin(dot(float2(id.xy), float2(12.9898, 78.233))) * 43758.5453);
    float occlusion = 0.0;

    for (uint i = 0; i < 12; i++)
    {
        float fraction = (float(i) + 0.5) / 12.0;
        float z = 0.15 + 0.85 * frac(fraction * 7.0);
        float radial = sqrt(1.0 - z * z);
        float phi = angle + float(i) * 2.3999632;
        float3 direction = tangent * (cos(phi) * radial) + bitangent * (sin(phi) * radial) + normal * z;
        float3 samplePosition = position + normal * 0.005 + direction * constants.RadiusInMeters * (0.2 + 0.8 * fraction * fraction);
        float4 clip = mul(float4(samplePosition, 1.0), constants.Projection);
        float2 uv = clip.xy / clip.w * float2(0.5, -0.5) + 0.5;
        if (clip.w <= 0.0 || any(uv <= 0.0) || any(uv >= 1.0))
        {
            continue;
        }

        int2 samplePixel = FullPixel(uv);
        float sampleDepth = DeviceDepth(samplePixel);
        if (sampleDepth >= 0.999999)
        {
            continue;
        }

        float3 scenePosition = ViewPosition(PixelUv(samplePixel), sampleDepth);
        float distance = length(scenePosition - position);
        float attenuation = 1.0 - smoothstep(constants.RadiusInMeters * 0.5, constants.RadiusInMeters, distance);
        occlusion += scenePosition.z > samplePosition.z + 0.005 ? attenuation : 0.0;
    }

    constants.OcclusionOutput[id.xy] = saturate(1.0 - constants.Strength * occlusion / 12.0);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Filter(uint3 id : SV_DispatchThreadID)
{
    if (any(id.xy >= constants.HalfSize))
    {
        return;
    }

    int2 centerPixel = FullPixel((float2(id.xy) + 0.5) / float2(constants.HalfSize));
    float centerDepth = DeviceDepth(centerPixel);
    if (centerDepth >= 0.999999)
    {
        constants.OcclusionOutput[id.xy] = 1.0;
        return;
    }

    float centerZ = ViewPosition(PixelUv(centerPixel), centerDepth).z;
    float weightedOcclusion = 0.0;
    float totalWeight = 0.0;
    for (int y = -2; y <= 2; y++)
    {
        for (int x = -2; x <= 2; x++)
        {
            int2 halfPixel = int2(id.xy) + int2(x, y);
            if (any(halfPixel < 0) || any(halfPixel >= int2(constants.HalfSize)))
            {
                continue;
            }

            int2 fullPixel = FullPixel((float2(halfPixel) + 0.5) / float2(constants.HalfSize));
            float sampleDepth = DeviceDepth(fullPixel);
            float sampleZ = ViewPosition(PixelUv(fullPixel), sampleDepth).z;
            float weight = exp(-0.5 * float(x * x + y * y)) * exp(-abs(sampleZ - centerZ) / (constants.RadiusInMeters * 0.2));
            weightedOcclusion += constants.Occlusion.Load(int3(halfPixel, 0)) * weight;
            totalWeight += weight;
        }
    }

    constants.OcclusionOutput[id.xy] = weightedOcclusion / max(totalWeight, 1e-5);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Composite(uint3 id : SV_DispatchThreadID)
{
    if (any(id.xy >= constants.RenderSize))
    {
        return;
    }

    float4 hdr = constants.HdrColor.Load(int3(id.xy, 0));
    float depth = DeviceDepth(int2(id.xy));
    if (depth >= 0.999999)
    {
        constants.HdrOutput[id.xy] = hdr;
        return;
    }

    float2 uv = PixelUv(int2(id.xy));
    float centerZ = ViewPosition(uv, depth).z;
    float2 halfPosition = uv * float2(constants.HalfSize) - 0.5;
    int2 basePixel = int2(floor(halfPosition));
    float2 fraction = frac(halfPosition);
    float weightedOcclusion = 0.0;
    float totalWeight = 0.0;
    for (int y = 0; y < 2; y++)
    {
        for (int x = 0; x < 2; x++)
        {
            int2 halfPixel = clamp(basePixel + int2(x, y), int2(0), int2(constants.HalfSize) - 1);
            int2 fullPixel = FullPixel((float2(halfPixel) + 0.5) / float2(constants.HalfSize));
            float sampleZ = ViewPosition(PixelUv(fullPixel), DeviceDepth(fullPixel)).z;
            float weight = (x == 0 ? 1.0 - fraction.x : fraction.x) * (y == 0 ? 1.0 - fraction.y : fraction.y);
            weight *= exp(-abs(sampleZ - centerZ) / (constants.RadiusInMeters * 0.15));
            weightedOcclusion += constants.Occlusion.Load(int3(halfPixel, 0)) * weight;
            totalWeight += weight;
        }
    }

    float ao = totalWeight > 1e-5 ? weightedOcclusion / totalWeight : 1.0;
    float3 indirectDiffuse = constants.IndirectDiffuse.Load(int3(id.xy, 0)).rgb;
    constants.HdrOutput[id.xy] = float4(max(hdr.rgb - indirectDiffuse * (1.0 - ao), 0.0), hdr.a);
}
