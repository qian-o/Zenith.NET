struct SkyParameters
{
    float4 SunDirectionAndIntensity;

    float4 SunRadiance;

    float4 ZenithColor;

    float4 HorizonColor;

    float4 GroundColor;
};

float3 EvaluateSky(float3 directionWorld, SkyParameters sky, bool includeSunDisk)
{
    float height = saturate(directionWorld.y);
    float3 upperSky = lerp(sky.HorizonColor.xyz, sky.ZenithColor.xyz, pow(height, 0.45));
    float groundBlend = smoothstep(-0.12, 0.04, directionWorld.y);
    float3 color = lerp(sky.GroundColor.xyz, upperSky, groundBlend) * sky.SunDirectionAndIntensity.w;

    float sunCosine = saturate(dot(directionWorld, sky.SunDirectionAndIntensity.xyz));
    float horizonHaze = exp(-height * 12.0) * groundBlend;
    color += sky.SunRadiance.xyz * (pow(sunCosine, 16.0) * 0.025 + pow(sunCosine, 256.0) * 0.07) * groundBlend;
    color += sky.HorizonColor.xyz * horizonHaze * sky.SunDirectionAndIntensity.w * 0.12;

    if (includeSunDisk)
    {
        float sunDisk = smoothstep(0.99993, 0.99997, sunCosine);
        color += sky.SunRadiance.xyz * sunDisk * 12.0;
    }

    return max(color, 0.0);
}
