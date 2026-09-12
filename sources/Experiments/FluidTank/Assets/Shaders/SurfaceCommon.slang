float3 SurfaceViewRay(float2 uv, float4x4 invProjection)
{
    float2 ndc = uv * float2(2.0, -2.0) + float2(-1.0, 1.0);
    float4 target = mul(float4(ndc, 1.0, 1.0), invProjection);

    return normalize(target.xyz / target.w);
}

float3 SurfaceViewPosition(float2 uv, float linearDepth, float4x4 invProjection)
{
    float3 ray = SurfaceViewRay(uv, invProjection);

    return ray * linearDepth / -ray.z;
}

float SurfaceDepthRange(float linearDepth)
{
    return 0.08 + linearDepth * 0.015;
}
