#include "FluidCommon.slang"

static const uint CellAir = 0;
static const uint CellFluid = 1;
static const uint CellSolid = 2;
static const float AccumulationScale = 65536.0;
static const float ParticleSpacingScale = 1.67;

struct SimulationConstants
{
    private float4 TankMinAndTimeStep;

    private float4 TankMaxAndTime;

    private float4 GridParameters;

    private float4 WaveAndInteraction;

    private float4 InteractionOriginAndRadius;

    private float4 InteractionDirectionAndDensity;

    private uint4 Counts;

    private uint4 GridAndPressure;

    private uint4 DamAndSubsteps;

    DescriptorHandle<RWStructuredBuffer<Particle>> Particles;

    DescriptorHandle<RWStructuredBuffer<float4>> PreviousPositions;

    DescriptorHandle<RWStructuredBuffer<float4>> ParticleAffine;

    DescriptorHandle<RWStructuredBuffer<int>> GridAccumulation;

    DescriptorHandle<RWStructuredBuffer<float4>> GridVelocity;

    DescriptorHandle<RWStructuredBuffer<float4>> GridVelocityOld;

    DescriptorHandle<RWStructuredBuffer<uint>> CellTypes;

    DescriptorHandle<RWStructuredBuffer<float>> Divergence;

    DescriptorHandle<RWStructuredBuffer<float>> PressureA;

    property float3 TankMin
    {
        get {
            return TankMinAndTimeStep.xyz;
        }
    }

    property float TimeStep
    {
        get {
            return TankMinAndTimeStep.w;
        }
    }

    property float3 TankMax
    {
        get {
            return TankMaxAndTime.xyz;
        }
    }

    property float Time
    {
        get {
            return TankMaxAndTime.w;
        }
    }

    property float GridSpacing
    {
        get {
            return GridParameters.x;
        }
    }

    property float InverseGridSpacing
    {
        get {
            return GridParameters.y;
        }
    }

    property float FlipRatio
    {
        get {
            return GridParameters.z;
        }
    }

    property float VelocityDamping
    {
        get {
            return GridParameters.w;
        }
    }

    property float WaveAmplitude
    {
        get {
            return WaveAndInteraction.x;
        }
    }

    property float WaveFrequency
    {
        get {
            return WaveAndInteraction.y;
        }
    }

    property float InteractionRadius
    {
        get {
            return WaveAndInteraction.z;
        }
    }

    property float InteractionStrength
    {
        get {
            return WaveAndInteraction.w;
        }
    }

    property float3 InteractionOrigin
    {
        get {
            return InteractionOriginAndRadius.xyz;
        }
    }

    property float ParticleRadius
    {
        get {
            return InteractionOriginAndRadius.w;
        }
    }

    property float3 InteractionDirection
    {
        get {
            return InteractionDirectionAndDensity.xyz;
        }
    }

    property float RestDensity
    {
        get {
            return InteractionDirectionAndDensity.w;
        }
    }

    property uint ParticleCount
    {
        get {
            return Counts.x;
        }
    }

    property uint CellCount
    {
        get {
            return Counts.y;
        }
    }

    property uint GridPointCount
    {
        get {
            return Counts.z;
        }
    }

    property bool WaveMakerEnabled
    {
        get {
            return Counts.w != 0;
        }
    }

    property uint3 GridDimensions
    {
        get {
            return GridAndPressure.xyz;
        }
    }

    property uint PressureIterations
    {
        get {
            return GridAndPressure.w;
        }
    }

    property uint3 DamDimensions
    {
        get {
            return DamAndSubsteps.xyz;
        }
    }

    property uint Substeps
    {
        get {
            return DamAndSubsteps.w;
        }
    }
};

ConstantBuffer<SimulationConstants> simulation;

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

bool CellInBounds(int3 cell)
{
    return all(cell >= 0) && all(cell < int3(simulation.GridDimensions));
}

bool GridPointInBounds(int3 point)
{
    return all(point >= 0) && all(point <= int3(simulation.GridDimensions));
}

uint CellIndex(int3 cell)
{
    return uint(cell.x) + uint(cell.y) * simulation.GridDimensions.x +
           uint(cell.z) * simulation.GridDimensions.x * simulation.GridDimensions.y;
}

uint GridIndex(int3 point)
{
    uint width = simulation.GridDimensions.x + 1;
    uint height = simulation.GridDimensions.y + 1;

    return uint(point.x) + uint(point.y) * width + uint(point.z) * width * height;
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

int3 PressureCellCoordinates(uint index, uint parity)
{
    uint pairsPerRow = (simulation.GridDimensions.x + 1) / 2;
    uint z = index / (pairsPerRow * simulation.GridDimensions.y);
    uint remainder = index - z * pairsPerRow * simulation.GridDimensions.y;
    uint y = remainder / pairsPerRow;
    uint pairX = remainder - y * pairsPerRow;
    uint x = pairX * 2 + ((y + z + parity) & 1);

    return int3(x, y, z);
}

int3 GridCoordinates(uint index)
{
    uint width = simulation.GridDimensions.x + 1;
    uint height = simulation.GridDimensions.y + 1;
    uint slice = width * height;
    uint z = index / slice;
    uint remainder = index - z * slice;
    uint y = remainder / width;
    uint x = remainder - y * width;

    return int3(x, y, z);
}

uint CellTypeAt(int3 cell)
{
    if (!CellInBounds(cell))
    {
        return CellSolid;
    }

    return simulation.CellTypes[CellIndex(cell)];
}

float PressureAt(int3 cell)
{
    return CellTypeAt(cell) == CellFluid ? simulation.PressureA[CellIndex(cell)] : 0.0;
}

float ParticleDensityAt(int3 cell)
{
    uint index = GridIndex(cell);
    uint width = simulation.GridDimensions.x + 1;
    uint slice = width * (simulation.GridDimensions.y + 1);
    float weight = float(simulation.GridAccumulation[simulation.GridPointCount + index]) +
                   float(simulation.GridAccumulation[simulation.GridPointCount + index + 1]) +
                   float(simulation.GridAccumulation[simulation.GridPointCount * 3 + index]) +
                   float(simulation.GridAccumulation[simulation.GridPointCount * 3 + index + width]) +
                   float(simulation.GridAccumulation[simulation.GridPointCount * 5 + index]) +
                   float(simulation.GridAccumulation[simulation.GridPointCount * 5 + index + slice]);

    return weight / (6.0 * AccumulationScale);
}

float3 FaceOffset(uint component)
{
    if (component == 0)
    {
        return float3(0.0, 0.5, 0.5);
    }

    if (component == 1)
    {
        return float3(0.5, 0.0, 0.5);
    }

    return float3(0.5, 0.5, 0.0);
}

bool FaceIsValid(uint component, int3 point)
{
    if (!GridPointInBounds(point))
    {
        return false;
    }

    if (component == 0)
    {
        return point.y < int(simulation.GridDimensions.y) && point.z < int(simulation.GridDimensions.z);
    }

    if (component == 1)
    {
        return point.x < int(simulation.GridDimensions.x) && point.z < int(simulation.GridDimensions.z);
    }

    return point.x < int(simulation.GridDimensions.x) && point.y < int(simulation.GridDimensions.y);
}

void FaceCells(uint component, int3 point, out int3 negativeCell, out int3 positiveCell)
{
    negativeCell = point;
    positiveCell = point;

    if (component == 0)
    {
        negativeCell.x--;
    }
    else if (component == 1)
    {
        negativeCell.y--;
    }
    else
    {
        negativeCell.z--;
    }
}

bool FaceIsOpen(uint component, int3 point)
{
    if (!FaceIsValid(component, point))
    {
        return false;
    }

    int3 negativeCell;
    int3 positiveCell;
    FaceCells(component, point, negativeCell, positiveCell);

    return CellTypeAt(negativeCell) != CellSolid && CellTypeAt(positiveCell) != CellSolid;
}

bool FaceTouchesFluid(uint component, int3 point)
{
    int3 negativeCell;
    int3 positiveCell;
    FaceCells(component, point, negativeCell, positiveCell);

    return CellTypeAt(negativeCell) == CellFluid || CellTypeAt(positiveCell) == CellFluid;
}

float3 QuadraticWeights(float value)
{
    return float3(0.5 * (1.5 - value) * (1.5 - value), 0.75 - (value - 1.0) * (value - 1.0),
                  0.5 * (value - 0.5) * (value - 0.5));
}

void ScatterComponent(float3 position, float3 velocity, float3 affineRow, uint component)
{
    float3 offset = FaceOffset(component);
    float3 coordinate = (position - simulation.TankMin) * simulation.InverseGridSpacing - offset;
    int3 base = int3(floor(coordinate - 0.5));
    float3 fractional = coordinate - float3(base);
    float3 weightX = QuadraticWeights(fractional.x);
    float3 weightY = QuadraticWeights(fractional.y);
    float3 weightZ = QuadraticWeights(fractional.z);

    for (int z = 0; z < 3; z++)
    {
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                int3 point = base + int3(x, y, z);
                if (!FaceIsValid(component, point))
                {
                    continue;
                }

                float weight = weightX[x] * weightY[y] * weightZ[z];
                float3 facePosition = simulation.TankMin + (float3(point) + offset) * simulation.GridSpacing;
                float momentum = clamp(velocity[component] + dot(affineRow, facePosition - position), -12.0, 12.0);
                uint index = GridIndex(point);
                uint valueOffset = component * 2 * simulation.GridPointCount;
                uint weightOffset = valueOffset + simulation.GridPointCount;
                int fixedWeight = int(round(weight * AccumulationScale));
                int fixedMomentum = int(round(momentum * weight * AccumulationScale));
                InterlockedAdd(simulation.GridAccumulation[valueOffset + index], fixedMomentum);
                InterlockedAdd(simulation.GridAccumulation[weightOffset + index], fixedWeight);
            }
        }
    }
}

bool FaceCanSample(uint component, int3 point)
{
    if (!FaceIsValid(component, point))
    {
        return false;
    }

    int3 negativeCell;
    int3 positiveCell;
    FaceCells(component, point, negativeCell, positiveCell);
    uint negativeType = CellTypeAt(negativeCell);
    uint positiveType = CellTypeAt(positiveCell);

    return negativeType != CellSolid && positiveType != CellSolid &&
           (negativeType == CellFluid || positiveType == CellFluid);
}

void SampleComponentPair(float3 position, uint component, out float currentVelocity, out float previousVelocity,
                         out float3 affineRow)
{
    float3 offset = FaceOffset(component);
    float3 coordinate = (position - simulation.TankMin) * simulation.InverseGridSpacing - offset;
    int3 base = int3(floor(coordinate - 0.5));
    float3 fractional = coordinate - float3(base);
    float3 weightX = QuadraticWeights(fractional.x);
    float3 weightY = QuadraticWeights(fractional.y);
    float3 weightZ = QuadraticWeights(fractional.z);
    currentVelocity = 0.0;
    previousVelocity = 0.0;
    float3 moment = float3(0.0, 0.0, 0.0);
    float3 firstMoment = float3(0.0, 0.0, 0.0);
    float3 secondMoment = float3(0.0, 0.0, 0.0);
    float weightSum = 0.0;

    for (int z = 0; z < 3; z++)
    {
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                int3 point = base + int3(x, y, z);
                if (!FaceCanSample(component, point))
                {
                    continue;
                }

                float weight = weightX[x] * weightY[y] * weightZ[z];
                uint index = GridIndex(point);
                float faceVelocity = simulation.GridVelocity[index][component];
                float oldFaceVelocity = simulation.GridVelocityOld[index][component];
                float3 facePosition = simulation.TankMin + (float3(point) + offset) * simulation.GridSpacing;
                float3 delta = facePosition - position;
                currentVelocity += weight * faceVelocity;
                previousVelocity += weight * oldFaceVelocity;
                moment += weight * faceVelocity * delta;
                firstMoment += weight * delta;
                secondMoment += weight * delta * delta;
                weightSum += weight;
            }
        }
    }

    if (weightSum <= 0.0001)
    {
        currentVelocity = 0.0;
        previousVelocity = 0.0;
        affineRow = float3(0.0, 0.0, 0.0);

        return;
    }

    currentVelocity /= weightSum;
    previousVelocity /= weightSum;
    float3 centeredMoment = moment - currentVelocity * firstMoment;
    float3 variance = secondMoment - firstMoment * firstMoment / weightSum;
    affineRow = centeredMoment / max(variance, simulation.GridSpacing * simulation.GridSpacing * 0.02);
    affineRow = clamp(affineRow, -24.0, 24.0);
}

float SampleGridComponent(float3 position, uint component)
{
    float3 offset = FaceOffset(component);
    float3 coordinate = (position - simulation.TankMin) * simulation.InverseGridSpacing - offset;
    int3 base = int3(floor(coordinate - 0.5));
    float3 fractional = coordinate - float3(base);
    float3 weightX = QuadraticWeights(fractional.x);
    float3 weightY = QuadraticWeights(fractional.y);
    float3 weightZ = QuadraticWeights(fractional.z);
    float velocity = 0.0;
    float weightSum = 0.0;

    for (int z = 0; z < 3; z++)
    {
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                int3 point = base + int3(x, y, z);
                if (!FaceCanSample(component, point))
                {
                    continue;
                }

                float weight = weightX[x] * weightY[y] * weightZ[z];
                velocity += weight * simulation.GridVelocity[GridIndex(point)][component];
                weightSum += weight;
            }
        }
    }

    return weightSum > 0.0001 ? velocity / weightSum : 0.0;
}

float3 SampleGridVelocity(float3 position)
{
    return float3(SampleGridComponent(position, 0), SampleGridComponent(position, 1), SampleGridComponent(position, 2));
}

bool ResolveCollisions(inout float3 position, inout float3 velocity)
{
    float3 minimum = simulation.TankMin + simulation.ParticleRadius;
    float3 maximum = simulation.TankMax - simulation.ParticleRadius;
    bool collided = false;

    if (position.x < minimum.x)
    {
        position.x = minimum.x;
        velocity.x = max(velocity.x, 0.0);
        collided = true;
    }
    else if (position.x > maximum.x)
    {
        position.x = maximum.x;
        velocity.x = min(velocity.x, 0.0);
        collided = true;
    }

    if (position.y < minimum.y)
    {
        position.y = minimum.y;
        velocity.y = max(velocity.y, 0.0);
        velocity.xz *= 0.998;
        collided = true;
    }
    else if (position.y > maximum.y)
    {
        position.y = maximum.y;
        velocity.y = min(velocity.y, 0.0);
        collided = true;
    }

    if (position.z < minimum.z)
    {
        position.z = minimum.z;
        velocity.z = max(velocity.z, 0.0);
        collided = true;
    }
    else if (position.z > maximum.z)
    {
        position.z = maximum.z;
        velocity.z = min(velocity.z, 0.0);
        collided = true;
    }

    float distance = ObstacleSdf(position);
    if (distance < simulation.ParticleRadius)
    {
        float3 normal = ObstacleNormal(position);
        position += normal * (simulation.ParticleRadius - distance);
        velocity -= normal * min(dot(velocity, normal), 0.0);
        velocity *= 0.999;
        collided = true;
    }

    position = clamp(position, minimum, maximum);

    return collided;
}

[shader("compute")]
[numthreads(256, 1, 1)]
void ResetCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.ParticleCount)
    {
        return;
    }

    uint x = index % simulation.DamDimensions.x;
    uint y = (index / simulation.DamDimensions.x) % simulation.DamDimensions.y;
    uint z = index / (simulation.DamDimensions.x * simulation.DamDimensions.y);
    float spacing = simulation.ParticleRadius * ParticleSpacingScale;
    float3 jitter = float3(Hash11(index * 3 + 0), Hash11(index * 3 + 1), Hash11(index * 3 + 2)) - 0.5;
    float3 position = simulation.TankMin + simulation.ParticleRadius * float3(1.05, 1.25, 1.25);
    position += float3(x, y, z) * spacing + jitter * spacing * 0.02;

    Particle particle;
    particle.PositionDensity = float4(position, simulation.RestDensity);
    particle.VelocityLambda = float4(0.0, 0.0, 0.0, 0.0);
    simulation.Particles[index] = particle;
    simulation.PreviousPositions[index] = float4(position, 0.0);
    simulation.ParticleAffine[index * 3 + 0] = float4(0.0, 0.0, 0.0, 0.0);
    simulation.ParticleAffine[index * 3 + 1] = float4(0.0, 0.0, 0.0, 0.0);
    simulation.ParticleAffine[index * 3 + 2] = float4(0.0, 0.0, 0.0, 0.0);
}

[shader("compute")]
[numthreads(256, 1, 1)]
void InitializeGridCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index < simulation.CellCount)
    {
        int3 cell = CellCoordinates(index);
        float3 center = simulation.TankMin + (float3(cell) + 0.5) * simulation.GridSpacing;
        simulation.CellTypes[index] = ObstacleSdf(center) < 0.0 ? CellSolid : CellAir;
        simulation.PressureA[index] = 0.0;
    }
}

[shader("compute")]
[numthreads(256, 1, 1)]
void ClearGridCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;

    if (index < simulation.GridPointCount * 6)
    {
        simulation.GridAccumulation[index] = 0;
    }

    if (index < simulation.CellCount)
    {
        simulation.CellTypes[index] = simulation.CellTypes[index] == CellSolid ? CellSolid : CellAir;
    }
}

void ParticleToGrid(uint index, bool savePreviousPosition)
{
    if (index >= simulation.ParticleCount)
    {
        return;
    }

    Particle particle = simulation.Particles[index];
    float3 position = ParticlePosition(particle);
    float3 velocity = ParticleVelocity(particle);

    if (savePreviousPosition)
    {
        simulation.PreviousPositions[index] = float4(position, 0.0);
    }

    int3 cell = clamp(int3(floor((position - simulation.TankMin) * simulation.InverseGridSpacing)), int3(0, 0, 0),
                      int3(simulation.GridDimensions) - 1);
    InterlockedMax(simulation.CellTypes[CellIndex(cell)], CellFluid);
    ScatterComponent(position, velocity, simulation.ParticleAffine[index * 3 + 0].xyz, 0);
    ScatterComponent(position, velocity, simulation.ParticleAffine[index * 3 + 1].xyz, 1);
    ScatterComponent(position, velocity, simulation.ParticleAffine[index * 3 + 2].xyz, 2);
}

[shader("compute")]
[numthreads(128, 1, 1)]
void BeginParticleToGridCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    ParticleToGrid(dispatchThreadID.x, true);
}

[shader("compute")]
[numthreads(128, 1, 1)]
void ParticleToGridCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    ParticleToGrid(dispatchThreadID.x, false);
}

[shader("compute")]
[numthreads(256, 1, 1)]
void NormalizeAndApplyForcesCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.GridPointCount)
    {
        return;
    }

    int3 point = GridCoordinates(index);
    float3 velocity = float3(0.0, 0.0, 0.0);

    for (uint component = 0; component < 3; component++)
    {
        uint valueOffset = component * 2 * simulation.GridPointCount;
        uint weightOffset = valueOffset + simulation.GridPointCount;
        int weight = simulation.GridAccumulation[weightOffset + index];

        if (weight > 0 && FaceIsOpen(component, point))
        {
            velocity[component] = float(simulation.GridAccumulation[valueOffset + index]) / float(weight);
        }
    }

    simulation.GridVelocityOld[index] = float4(velocity, 0.0);

    if (FaceIsOpen(1, point) && FaceTouchesFluid(1, point))
    {
        velocity.y -= 9.81 * simulation.TimeStep;
    }

    if (simulation.WaveMakerEnabled && point.x <= 2 && FaceIsOpen(0, point) && FaceTouchesFluid(0, point))
    {
        float phase = simulation.Time * simulation.WaveFrequency * 2.0 * PI + float(point.z) * 0.13;
        float targetVelocity = simulation.WaveAmplitude * simulation.WaveFrequency * 2.0 * PI * cos(phase);
        velocity.x = lerp(velocity.x, targetVelocity, exp(-float(point.x) * 0.65));
    }

    if (simulation.InteractionStrength > 0.0)
    {
        for (uint component = 0; component < 3; component++)
        {
            if (!FaceIsOpen(component, point) || !FaceTouchesFluid(component, point))
            {
                continue;
            }

            float3 facePosition = simulation.TankMin + (float3(point) + FaceOffset(component)) * simulation.GridSpacing;
            float rayDistance = dot(facePosition - simulation.InteractionOrigin, simulation.InteractionDirection);
            float3 closestPoint =
                simulation.InteractionOrigin + simulation.InteractionDirection * max(rayDistance, 0.0);
            float distance = length(facePosition - closestPoint);

            if (rayDistance > 0.0 && distance < simulation.InteractionRadius)
            {
                float falloff = 1.0 - distance / simulation.InteractionRadius;
                velocity[component] += (simulation.InteractionDirection[component] + (component == 1 ? 0.45 : 0.0)) *
                                       simulation.InteractionStrength * falloff / float(simulation.Substeps);
            }
        }
    }

    simulation.GridVelocity[index] = float4(velocity, 0.0);
}

[shader("compute")]
[numthreads(256, 1, 1)]
void DivergenceCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.CellCount)
    {
        return;
    }

    if (simulation.CellTypes[index] != CellFluid)
    {
        simulation.Divergence[index] = 0.0;
        simulation.PressureA[index] = 0.0;

        return;
    }

    int3 cell = CellCoordinates(index);
    float3 negativeVelocity = simulation.GridVelocity[GridIndex(cell)].xyz;
    float velocityLeft = negativeVelocity.x;
    float velocityRight = simulation.GridVelocity[GridIndex(cell + int3(1, 0, 0))].x;
    float velocityBottom = negativeVelocity.y;
    float velocityTop = simulation.GridVelocity[GridIndex(cell + int3(0, 1, 0))].y;
    float velocityBack = negativeVelocity.z;
    float velocityFront = simulation.GridVelocity[GridIndex(cell + int3(0, 0, 1))].z;
    float divergence = (velocityRight - velocityLeft + velocityTop - velocityBottom + velocityFront - velocityBack) *
                       simulation.InverseGridSpacing;
    float particleSpacing = simulation.ParticleRadius * ParticleSpacingScale;
    float targetDensity = simulation.GridSpacing * simulation.GridSpacing * simulation.GridSpacing /
                          (particleSpacing * particleSpacing * particleSpacing);
    float compression = max(ParticleDensityAt(cell) / targetDensity - 1.0, 0.0);
    float expansion = min(compression * 0.1 / simulation.TimeStep, 2.0);
    simulation.Divergence[index] = divergence - expansion;
}

void RelaxPressure(int3 cell)
{
    if (any(cell >= int3(simulation.GridDimensions)))
    {
        return;
    }

    uint index = CellIndex(cell);
    if (simulation.CellTypes[index] != CellFluid)
    {
        return;
    }

    int3 neighbors[6] = { cell + int3(-1, 0, 0), cell + int3(1, 0, 0),  cell + int3(0, -1, 0),
                          cell + int3(0, 1, 0),  cell + int3(0, 0, -1), cell + int3(0, 0, 1) };
    float sum = 0.0;
    float denominator = 0.0;

    for (uint neighborIndex = 0; neighborIndex < 6; neighborIndex++)
    {
        uint type = CellTypeAt(neighbors[neighborIndex]);
        if (type == CellSolid)
        {
            continue;
        }

        denominator += 1.0;
        if (type == CellFluid)
        {
            sum += simulation.PressureA[CellIndex(neighbors[neighborIndex])];
        }
    }

    float rightHandSide =
        simulation.Divergence[index] * simulation.GridSpacing * simulation.GridSpacing / simulation.TimeStep;
    float pressure = denominator > 0.0 ? (sum - rightHandSide) / denominator : 0.0;
    simulation.PressureA[index] = lerp(simulation.PressureA[index], pressure, 1.6);
}

[shader("compute")]
[numthreads(256, 1, 1)]
void PressureRedCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    RelaxPressure(PressureCellCoordinates(dispatchThreadID.x, 0));
}

[shader("compute")]
[numthreads(256, 1, 1)]
void PressureBlackCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    RelaxPressure(PressureCellCoordinates(dispatchThreadID.x, 1));
}

[shader("compute")]
[numthreads(256, 1, 1)]
void ProjectGridCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.GridPointCount)
    {
        return;
    }

    int3 point = GridCoordinates(index);
    float3 velocity = simulation.GridVelocity[index].xyz;

    for (uint component = 0; component < 3; component++)
    {
        int3 negativeCell;
        int3 positiveCell;
        FaceCells(component, point, negativeCell, positiveCell);
        uint negativeType = CellTypeAt(negativeCell);
        uint positiveType = CellTypeAt(positiveCell);

        if (negativeType == CellSolid || positiveType == CellSolid)
        {
            velocity[component] = 0.0;
            continue;
        }

        if (negativeType != CellFluid && positiveType != CellFluid)
        {
            continue;
        }

        float negativePressure = negativeType == CellFluid ? simulation.PressureA[CellIndex(negativeCell)] : 0.0;
        float positivePressure = positiveType == CellFluid ? simulation.PressureA[CellIndex(positiveCell)] : 0.0;
        velocity[component] -=
            simulation.TimeStep * (positivePressure - negativePressure) * simulation.InverseGridSpacing;
        velocity[component] = clamp(velocity[component], -8.0, 8.0);
    }

    simulation.GridVelocity[index] = float4(velocity, 0.0);
}

[shader("compute")]
[numthreads(128, 1, 1)]
void GridToParticleAndAdvectCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;
    if (index >= simulation.ParticleCount)
    {
        return;
    }

    Particle particle = simulation.Particles[index];
    float3 position = ParticlePosition(particle);
    float3 oldVelocity = ParticleVelocity(particle);
    float3 affineX;
    float3 affineY;
    float3 affineZ;
    float3 picVelocity;
    float3 oldGridVelocity;
    SampleComponentPair(position, 0, picVelocity.x, oldGridVelocity.x, affineX);
    SampleComponentPair(position, 1, picVelocity.y, oldGridVelocity.y, affineY);
    SampleComponentPair(position, 2, picVelocity.z, oldGridVelocity.z, affineZ);
    float3 flipVelocity = oldVelocity + picVelocity - oldGridVelocity;
    float damping = pow(simulation.VelocityDamping, simulation.TimeStep * 60.0);
    float3 velocity = lerp(picVelocity, flipVelocity, simulation.FlipRatio) * damping;
    float speed = length(velocity);

    if (speed > 7.5)
    {
        velocity *= 7.5 / speed;
    }

    if (speed < 0.012 && !simulation.WaveMakerEnabled && simulation.InteractionStrength <= 0.0)
    {
        velocity = float3(0.0, 0.0, 0.0);
    }

    float3 midpoint = position + picVelocity * simulation.TimeStep * 0.5;
    float3 advectionVelocity = SampleGridVelocity(midpoint);
    position += advectionVelocity * simulation.TimeStep;
    bool collided = ResolveCollisions(position, velocity);
    particle.PositionDensity = float4(position, simulation.RestDensity);
    particle.VelocityLambda = float4(velocity, 0.0);
    simulation.Particles[index] = particle;

    if (collided)
    {
        simulation.ParticleAffine[index * 3 + 0] = float4(0.0, 0.0, 0.0, 0.0);
        simulation.ParticleAffine[index * 3 + 1] = float4(0.0, 0.0, 0.0, 0.0);
        simulation.ParticleAffine[index * 3 + 2] = float4(0.0, 0.0, 0.0, 0.0);
    }
    else
    {
        simulation.ParticleAffine[index * 3 + 0] = float4(affineX, 0.0);
        simulation.ParticleAffine[index * 3 + 1] = float4(affineY, 0.0);
        simulation.ParticleAffine[index * 3 + 2] = float4(affineZ, 0.0);
    }
}
