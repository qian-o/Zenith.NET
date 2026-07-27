static const float PI = 3.14159265359;

struct Particle
{
    float4 PositionDensity;
    float4 VelocityLambda;
};

float3 ParticlePosition(Particle particle)
{
    return particle.PositionDensity.xyz;
}

float3 ParticleVelocity(Particle particle)
{
    return particle.VelocityLambda.xyz;
}

float Hash11(uint value)
{
    value ^= value >> 16;
    value *= 0x7feb352du;
    value ^= value >> 15;
    value *= 0x846ca68bu;
    value ^= value >> 16;

    return float(value) / 4294967295.0;
}