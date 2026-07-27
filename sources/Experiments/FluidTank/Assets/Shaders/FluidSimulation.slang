#include "FluidCommon.slang"

struct SimulationConstants
{
    private float4 TankMinAndTimeStep;
    private float4 TankMaxAndTime;
    private float4 FluidParameters;
    private float4 ForceParameters;
    private float4 InteractionOriginAndRadius;
    private float4 InteractionDirectionAndStrength;
    private uint4 Counts;
    private uint4 GridAndIterations;
    private uint4 DamAndSubsteps;

    DescriptorHandle<RWStructuredBuffer<Particle>> Particles;
    DescriptorHandle<RWStructuredBuffer<float4>> PreviousPositions;
    DescriptorHandle<RWStructuredBuffer<float4>> DeltaPositions;
    DescriptorHandle<RWStructuredBuffer<int>> GridHeads;
    DescriptorHandle<RWStructuredBuffer<int>> ParticleNext;
    DescriptorHandle<RWStructuredBuffer<float4>> NewVelocities;

    property float3 TankMin { get { return TankMinAndTimeStep.xyz; } }
    property float TimeStep { get { return TankMinAndTimeStep.w; } }
    property float3 TankMax { get { return TankMaxAndTime.xyz; } }
    property float Time { get { return TankMaxAndTime.w; } }
    property float ParticleRadius { get { return FluidParameters.x; } }
    property float SmoothingRadius { get { return FluidParameters.y; } }
    property float RestDensity { get { return FluidParameters.z; } }
    property float Relaxation { get { return FluidParameters.w; } }
    property float Viscosity { get { return ForceParameters.x; } }
    property float SurfaceTension { get { return ForceParameters.y; } }
    property float WaveAmplitude { get { return ForceParameters.z; } }
    property float WaveFrequency { get { return ForceParameters.w; } }
    property float3 InteractionOrigin { get { return InteractionOriginAndRadius.xyz; } }
    property float InteractionRadius { get { return InteractionOriginAndRadius.w; } }
    property float3 InteractionDirection { get { return InteractionDirectionAndStrength.xyz; } }
    property float InteractionStrength { get { return InteractionDirectionAndStrength.w; } }
    property uint ParticleCount { get { return Counts.x; } }
    property uint CellCount { get { return Counts.y; } }
    property bool WaveMakerEnabled { get { return Counts.z != 0; } }
    property uint3 GridDimensions { get { return GridAndIterations.xyz; } }
    property uint SolverIterations { get { return GridAndIterations.w; } }
    property uint3 DamDimensions { get { return DamAndSubsteps.xyz; } }
    property uint Substeps { get { return DamAndSubsteps.w; } }
};

ConstantBuffer<SimulationConstants> simulation;

float Kernel(float3 offset)
{
    float q2 = dot(offset, offset) / (simulation.SmoothingRadius * simulation.SmoothingRadius);

    if (q2 >= 1.0)
    {
        return 0.0;
    }

    float value = 1.0 - q2;

    return value * value * value;
}

float3 KernelGradient(float3 offset)
{
    float radius2 = simulation.SmoothingRadius * simulation.SmoothingRadius;
    float q2 = dot(offset, offset) / radius2;

    if (q2 >= 1.0 || q2 <= 0.000001)
    {
        return float3(0.0, 0.0, 0.0);
    }

    float value = 1.0 - q2;

    return -6.0 * value * value * offset / radius2;
}

int3 PositionToCell(float3 position)
{
    int3 cell = int3(floor((position - simulation.TankMin) / simulation.SmoothingRadius));

    return clamp(cell, int3(0, 0, 0), int3(simulation.GridDimensions) - 1);
}

uint CellIndex(int3 cell)
{
    return uint(cell.x) + uint(cell.y) * simulation.GridDimensions.x + uint(cell.z) * simulation.GridDimensions.x * simulation.GridDimensions.y;
}

int3 CellCoordinates(uint index)
{
    uint slice = simulation.GridDimensions.x * simulation.GridDimensions.y;
    uint z = index / slice;
    uint remainder = index - z * slice;
    uint y = remainder / simulation.GridDimensions.x;
    uint x = remainder - y * simulation.GridDimensions.x;

    return int3(x, y, z);
}

float RoundedBoxSdf(float3 position, float3 center, float3 halfExtents, float radius)
{
    float3 q = abs(position - center) - halfExtents + radius;

    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0) - radius;
}

float CylinderSdf(float3 position, float3 center, float radius, float halfHeight)
{
    float2 d = abs(float2(length(position.xz - center.xz), position.y - center.y)) - float2(radius, halfHeight);

    return min(max(d.x, d.y), 0.0) + length(max(d, 0.0));
}

float OrientedBoxSdf(float3 position, float3 center, float3 halfExtents, float angle)
{
    float cosine = cos(angle);
    float sine = sin(angle);
    float3 local = position - center;
    local.xy = float2(cosine * local.x + sine * local.y, -sine * local.x + cosine * local.y);

    float3 q = abs(local) - halfExtents;

    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

float ObstacleSdf(float3 position)
{
    float roundedBox = RoundedBoxSdf(position, float3(-1.45, 0.82, 0.95), float3(0.72, 0.82, 0.72), 0.0);
    float cylinder = CylinderSdf(position, float3(1.15, 0.92, -0.85), 0.62, 0.92);
    float ramp = OrientedBoxSdf(position, float3(3.45, 0.62, 0.55), float3(1.15, 0.16, 1.05), -0.35);

    return min(roundedBox, min(cylinder, ramp));
}

float3 ObstacleNormal(float3 position)
{
    float epsilon = simulation.ParticleRadius * 0.1;
    float3 x = float3(epsilon, 0.0, 0.0);
    float3 y = float3(0.0, epsilon, 0.0);
    float3 z = float3(0.0, 0.0, epsilon);

    return normalize(float3(ObstacleSdf(position + x) - ObstacleSdf(position - x),
                            ObstacleSdf(position + y) - ObstacleSdf(position - y),
                            ObstacleSdf(position + z) - ObstacleSdf(position - z)));
}

float3 ConstrainPosition(float3 position)
{
    float radius = simulation.ParticleRadius;
    float wave = 0.0;

    if (simulation.WaveMakerEnabled)
    {
        float phase = simulation.Time * simulation.WaveFrequency * 2.0 * PI + position.z * 0.48;
        wave = simulation.WaveAmplitude * (0.5 + 0.5 * sin(phase));
    }

    float3 minimum = simulation.TankMin + radius;
    minimum.x += wave;
    float3 maximum = simulation.TankMax - radius;

    position = clamp(position, minimum, maximum);

    float distance = ObstacleSdf(position);
    if (distance < radius)
    {
        position += ObstacleNormal(position) * (radius - distance);
    }

    return clamp(position, minimum, maximum);
}

[shader("compute")]
[numthreads(256, 1, 1)]
void ResetCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.ParticleCount)
    {
        return;
    }

    uint x = index % simulation.DamDimensions.x;
    uint y = (index / simulation.DamDimensions.x) % simulation.DamDimensions.y;
    uint z = index / (simulation.DamDimensions.x * simulation.DamDimensions.y);

    float spacing = simulation.ParticleRadius * 1.70;
    float3 jitter = float3(Hash11(index * 3 + 0), Hash11(index * 3 + 1), Hash11(index * 3 + 2)) - 0.5;
    float3 position = simulation.TankMin + simulation.ParticleRadius * float3(1.05, 1.25, 1.25);
    position += float3(x, y, z) * spacing + jitter * spacing * 0.035;

    Particle particle;
    particle.PositionDensity = float4(position, simulation.RestDensity);
    particle.VelocityLambda = float4(0.0, 0.0, 0.0, 0.0);
    simulation.Particles[index] = particle;
    simulation.PreviousPositions[index] = float4(position, 0.0);
    simulation.DeltaPositions[index] = float4(0.0, 0.0, 0.0, 0.0);
    simulation.NewVelocities[index] = float4(0.0, 0.0, 0.0, 0.0);
    simulation.ParticleNext[index] = -1;
}

[shader("compute")]
[numthreads(256, 1, 1)]
void ClearGridCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index < simulation.CellCount)
    {
        simulation.GridHeads[index] = -1;
    }
}

[shader("compute")]
[numthreads(256, 1, 1)]
void PredictCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.ParticleCount)
    {
        return;
    }

    Particle particle = simulation.Particles[index];
    float3 position = ParticlePosition(particle);
    float3 velocity = ParticleVelocity(particle);

    simulation.PreviousPositions[index] = float4(position, 0.0);

    velocity += float3(0.0, -9.81, 0.0) * simulation.TimeStep;

    float rayDistance = dot(position - simulation.InteractionOrigin, simulation.InteractionDirection);
    float3 closestPoint = simulation.InteractionOrigin + simulation.InteractionDirection * max(rayDistance, 0.0);
    float interactionDistance = length(position - closestPoint);

    if (simulation.InteractionStrength > 0.0 && rayDistance > 0.0 && interactionDistance < simulation.InteractionRadius)
    {
        float falloff = 1.0 - interactionDistance / simulation.InteractionRadius;
        velocity += (simulation.InteractionDirection + float3(0.0, 0.45, 0.0)) * simulation.InteractionStrength * falloff;
    }

    position = ConstrainPosition(position + velocity * simulation.TimeStep);

    particle.PositionDensity = float4(position, particle.PositionDensity.w);
    particle.VelocityLambda = float4(velocity, particle.VelocityLambda.w);
    simulation.Particles[index] = particle;
}

[shader("compute")]
[numthreads(256, 1, 1)]
void BuildGridCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.ParticleCount)
    {
        return;
    }

    uint cell = CellIndex(PositionToCell(ParticlePosition(simulation.Particles[index])));
    int previousHead;
    InterlockedExchange(simulation.GridHeads[cell], int(index), previousHead);
    simulation.ParticleNext[index] = previousHead;
    float4 velocityData = simulation.NewVelocities[index];
    velocityData.w = asfloat(cell);
    simulation.NewVelocities[index] = velocityData;
}

[shader("compute")]
[numthreads(128, 1, 1)]
void DensityCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.ParticleCount)
    {
        return;
    }

    Particle particle = simulation.Particles[index];
    float3 position = ParticlePosition(particle);
    int3 centerCell = CellCoordinates(asuint(simulation.NewVelocities[index].w));
    float density = 0.0;
    float3 gradientI = float3(0.0, 0.0, 0.0);
    float gradientSum = 0.0;

    for (int z = -1; z <= 1; z++)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                int3 cell = centerCell + int3(x, y, z);
                if (any(cell < 0) || any(cell >= int3(simulation.GridDimensions)))
                {
                    continue;
                }

                int neighbor = simulation.GridHeads[CellIndex(cell)];
                while (neighbor >= 0)
                {
                    float3 offset = position - ParticlePosition(simulation.Particles[neighbor]);
                    float weight = Kernel(offset);
                    density += weight;

                    if (uint(neighbor) != index && weight > 0.0)
                    {
                        float3 gradient = KernelGradient(offset) / simulation.RestDensity;
                        gradientI += gradient;
                        gradientSum += dot(gradient, gradient);
                    }

                    neighbor = simulation.ParticleNext[neighbor];
                }
            }
        }
    }

    gradientSum += dot(gradientI, gradientI);
    float constraint = max(density / simulation.RestDensity - 1.0, 0.0);
    float lambda = -constraint / (gradientSum + simulation.Relaxation);

    particle.PositionDensity.w = density;
    particle.VelocityLambda.w = lambda;
    simulation.Particles[index] = particle;
}

[shader("compute")]
[numthreads(128, 1, 1)]
void DeltaCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.ParticleCount)
    {
        return;
    }

    Particle particle = simulation.Particles[index];
    float3 position = ParticlePosition(particle);
    int3 centerCell = CellCoordinates(asuint(simulation.NewVelocities[index].w));
    float3 delta = float3(0.0, 0.0, 0.0);
    float3 viscosity = float3(0.0, 0.0, 0.0);
    float3 cohesion = float3(0.0, 0.0, 0.0);
    float referenceKernel = Kernel(float3(simulation.SmoothingRadius * 0.30, 0.0, 0.0));

    for (int z = -1; z <= 1; z++)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                int3 cell = centerCell + int3(x, y, z);
                if (any(cell < 0) || any(cell >= int3(simulation.GridDimensions)))
                {
                    continue;
                }

                int neighbor = simulation.GridHeads[CellIndex(cell)];
                while (neighbor >= 0)
                {
                    if (uint(neighbor) != index)
                    {
                        Particle neighborParticle = simulation.Particles[neighbor];
                        float3 offset = position - ParticlePosition(neighborParticle);
                        float weight = Kernel(offset);

                        if (weight > 0.0)
                        {
                            float ratio = weight / max(referenceKernel, 0.0001);
                            float correction = -0.0025 * ratio * ratio * ratio * ratio;
                            delta += (particle.VelocityLambda.w + neighborParticle.VelocityLambda.w + correction) * KernelGradient(offset);
                            viscosity += (ParticleVelocity(neighborParticle) - ParticleVelocity(particle)) * weight;
                            cohesion -= offset * weight;
                        }
                    }

                    neighbor = simulation.ParticleNext[neighbor];
                }
            }
        }
    }

    simulation.DeltaPositions[index] = float4(delta / simulation.RestDensity, 0.0);
    simulation.NewVelocities[index] = float4(viscosity * simulation.Viscosity + cohesion * simulation.SurfaceTension * simulation.TimeStep,
                                             simulation.NewVelocities[index].w);
}

[shader("compute")]
[numthreads(256, 1, 1)]
void ApplyDeltaCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.ParticleCount)
    {
        return;
    }

    Particle particle = simulation.Particles[index];
    float3 delta = simulation.DeltaPositions[index].xyz;
    float deltaLength = length(delta);
    float maximumDelta = simulation.ParticleRadius * 0.32;
    if (deltaLength > maximumDelta)
    {
        delta *= maximumDelta / deltaLength;
    }

    float3 position = ConstrainPosition(ParticlePosition(particle) + delta);
    particle.PositionDensity.xyz = position;
    simulation.Particles[index] = particle;
}

[shader("compute")]
[numthreads(128, 1, 1)]
void VelocityCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.ParticleCount)
    {
        return;
    }

    Particle particle = simulation.Particles[index];
    float3 position = ParticlePosition(particle);
    float3 velocity = (position - simulation.PreviousPositions[index].xyz) / simulation.TimeStep + simulation.NewVelocities[index].xyz;
    float floorHeight = simulation.TankMin.y + simulation.ParticleRadius;
    float floorContact = 1.0 - smoothstep(floorHeight, floorHeight + simulation.ParticleRadius * 0.6, position.y);
    velocity.y = lerp(velocity.y, max(velocity.y, 0.0) * 0.08, floorContact);
    velocity.y = floorContact > 0.9 && abs(velocity.y) < 0.08 ? 0.0 : velocity.y;
    velocity.xz *= 1.0 - floorContact * 0.14;
    velocity *= 0.995;

    float speed = length(velocity);
    if (speed > 6.5)
    {
        velocity *= 6.5 / speed;
    }

    simulation.NewVelocities[index] = float4(velocity, 0.0);
}

[shader("compute")]
[numthreads(256, 1, 1)]
void CommitVelocityCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.ParticleCount)
    {
        return;
    }

    Particle particle = simulation.Particles[index];
    particle.VelocityLambda.xyz = simulation.NewVelocities[index].xyz;
    simulation.Particles[index] = particle;
}