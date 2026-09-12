#include "FluidCommon.slang"
#include "SurfaceCommon.slang"

struct DensitySurfaceConstants
{
    float4x4 View;

    float4x4 InvView;

    float4x4 InvProjection;

    private float4 OriginAndCellSize;

    private float4 MinimumAndInverseCellSize;

    private float4 MaximumAndIsoValue;

    private uint4 GridAndParticleCount;

    private uint4 ScreenAndParameters;

    DescriptorHandle<StructuredBuffer<Particle>> Particles;

    DescriptorHandle<StructuredBuffer<float4>> PreviousPositions;

    DescriptorHandle<RWStructuredBuffer<int>> Accumulation;

    DescriptorHandle<RWTexture3D<float>> DensityWrite;

    DescriptorHandle<Texture3D<float>> DensityRead;

    DescriptorHandle<RWTexture3D<float4>> FieldWrite;

    DescriptorHandle<Texture3D<float4>> FieldRead;

    DescriptorHandle<RWTexture3D<float>> OccupancyWrite;

    DescriptorHandle<Texture3D<float>> OccupancyRead;

    DescriptorHandle<Texture2D<float>> SceneDepth;

    DescriptorHandle<RWTexture2D<float>> DepthOutput;

    DescriptorHandle<RWTexture2D<float>> ThicknessOutput;

    DescriptorHandle<RWTexture2D<float4>> NormalOutput;

    DescriptorHandle<SamplerState> Sampler;

    property float3 Origin
    {
        get
        {
            return OriginAndCellSize.xyz;
        }
    }

    property float CellSize
    {
        get
        {
            return OriginAndCellSize.w;
        }
    }

    property float3 Minimum
    {
        get
        {
            return MinimumAndInverseCellSize.xyz;
        }
    }

    property float InverseCellSize
    {
        get
        {
            return MinimumAndInverseCellSize.w;
        }
    }

    property float3 Maximum
    {
        get
        {
            return MaximumAndIsoValue.xyz;
        }
    }

    property float IsoValue
    {
        get
        {
            return MaximumAndIsoValue.w;
        }
    }

    property uint3 GridDimensions
    {
        get
        {
            return GridAndParticleCount.xyz;
        }
    }

    property uint ParticleCount
    {
        get
        {
            return GridAndParticleCount.w;
        }
    }

    property uint2 ScreenDimensions
    {
        get
        {
            return ScreenAndParameters.xy;
        }
    }

    property float InterpolationAlpha
    {
        get
        {
            return asfloat(ScreenAndParameters.z);
        }
    }

    property float VolumeScale
    {
        get
        {
            return asfloat(ScreenAndParameters.w);
        }
    }
};

ConstantBuffer<DensitySurfaceConstants> densitySurface;

groupshared float occupancyValues[64];

uint GridIndex(int3 position)
{
    uint3 dimensions = densitySurface.GridDimensions;

    return uint(position.x) + dimensions.x * (uint(position.y) + dimensions.y * uint(position.z));
}

int3 ClampGrid(int3 position)
{
    return clamp(position, int3(0), int3(densitySurface.GridDimensions) - 1);
}

float3 QuadraticWeights(float fraction)
{
    return float3(0.5 * (1.5 - fraction) * (1.5 - fraction), 0.75 - (fraction - 1.0) * (fraction - 1.0), 0.5 * (fraction - 0.5) * (fraction - 0.5));
}

void DepositPosition(float3 position)
{
    float3 gridPosition = (position - densitySurface.Origin) * densitySurface.InverseCellSize - 0.5;
    int3 base = int3(floor(gridPosition - 0.5));
    float3 fraction = gridPosition - float3(base);
    float3 weightsX = QuadraticWeights(fraction.x);
    float3 weightsY = QuadraticWeights(fraction.y);
    float3 weightsZ = QuadraticWeights(fraction.z);

    for (int z = 0; z < 3; z++)
    {
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                int3 cell = base + int3(x, y, z);
                float weight = weightsX[x] * weightsY[y] * weightsZ[z] * densitySurface.VolumeScale;
                int contribution = int(round(weight * 65536.0));
                InterlockedAdd(densitySurface.Accumulation[GridIndex(cell)], contribution);
            }
        }
    }
}

float ReadDensity(int3 position)
{
    return densitySurface.DensityRead.Load(int4(ClampGrid(position), 0));
}

float4 SampleField(float3 position)
{
    float3 uv = (position - densitySurface.Origin) * densitySurface.InverseCellSize / float3(densitySurface.GridDimensions);

    return densitySurface.FieldRead.SampleLevel(densitySurface.Sampler, uv, 0.0);
}

bool IntersectTank(float3 origin, float3 direction, out float nearDistance, out float farDistance, out float3 entryNormal)
{
    nearDistance = -1.0e30;
    farDistance = 1.0e30;
    entryNormal = float3(0.0);

    for (int axis = 0; axis < 3; axis++)
    {
        if (direction[axis] == 0.0)
        {
            if (origin[axis] < densitySurface.Minimum[axis] || origin[axis] > densitySurface.Maximum[axis])
            {
                return false;
            }

            continue;
        }

        float distance0 = (densitySurface.Minimum[axis] - origin[axis]) / direction[axis];
        float distance1 = (densitySurface.Maximum[axis] - origin[axis]) / direction[axis];
        float axisNear = min(distance0, distance1);

        if (axisNear > nearDistance)
        {
            nearDistance = axisNear;
            entryNormal = float3(0.0);
            entryNormal[axis] = direction[axis] > 0.0 ? -1.0 : 1.0;
        }

        farDistance = min(farDistance, max(distance0, distance1));
    }

    return farDistance > max(nearDistance, 0.0);
}

int3 OccupancyBrick(float3 position, float3 direction)
{
    float3 gridPosition = (position - densitySurface.Origin) * densitySurface.InverseCellSize - 0.5;

    return int3(floor((gridPosition + sign(direction) * 0.0001) * 0.25));
}

float BrickExitDistance(float3 origin, float3 direction, int3 brick)
{
    float distance = 1.0e30;

    for (int axis = 0; axis < 3; axis++)
    {
        if (direction[axis] == 0.0)
        {
            continue;
        }

        float boundary = densitySurface.Origin[axis] + (float(brick[axis] * 4 + (direction[axis] > 0.0 ? 4 : 0)) + 0.5) * densitySurface.CellSize;
        distance = min(distance, (boundary - origin[axis]) / direction[axis]);
    }

    return distance;
}

float RefineIntersection(float3 origin, float3 direction, float nearDistance, float farDistance, bool nearInside)
{
    for (int iteration = 0; iteration < 5; iteration++)
    {
        float middle = (nearDistance + farDistance) * 0.5;
        bool inside = SampleField(origin + direction * middle).w >= densitySurface.IsoValue;

        if (inside == nearInside)
        {
            nearDistance = middle;
        }
        else
        {
            farDistance = middle;
        }
    }

    return (nearDistance + farDistance) * 0.5;
}

[shader("compute")]
[numthreads(256, 1, 1)]
void ClearCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint3 dimensions = densitySurface.GridDimensions;

    if (dispatchThreadID.x < dimensions.x * dimensions.y * dimensions.z)
    {
        densitySurface.Accumulation[dispatchThreadID.x] = 0;
    }
}

[shader("compute")]
[numthreads(128, 1, 1)]
void DepositCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint index = dispatchThreadID.x;

    if (index >= densitySurface.ParticleCount)
    {
        return;
    }

    float3 position = lerp(densitySurface.PreviousPositions[index].xyz, ParticlePosition(densitySurface.Particles[index]), densitySurface.InterpolationAlpha);
    float3 mirrored = position;
    uint3 copies = uint3(1);
    float mirrorRange = densitySurface.CellSize * 2.5;

    for (int axis = 0; axis < 3; axis++)
    {
        if (position[axis] - densitySurface.Minimum[axis] < mirrorRange)
        {
            mirrored[axis] = 2.0 * densitySurface.Minimum[axis] - position[axis];
            copies[axis] = 2;
        }
        else if (axis != 1 && densitySurface.Maximum[axis] - position[axis] < mirrorRange)
        {
            mirrored[axis] = 2.0 * densitySurface.Maximum[axis] - position[axis];
            copies[axis] = 2;
        }
    }

    for (uint z = 0; z < copies.z; z++)
    {
        for (uint y = 0; y < copies.y; y++)
        {
            for (uint x = 0; x < copies.x; x++)
            {
                DepositPosition(float3(x == 0 ? position.x : mirrored.x, y == 0 ? position.y : mirrored.y, z == 0 ? position.z : mirrored.z));
            }
        }
    }
}

[shader("compute")]
[numthreads(4, 4, 4)]
void SmoothCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    int3 cell = int3(dispatchThreadID);
    float density = 0.0;

    for (int z = -1; z <= 1; z++)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                float weight = float((x == 0 ? 2 : 1) * (y == 0 ? 2 : 1) * (z == 0 ? 2 : 1));
                density += float(densitySurface.Accumulation[GridIndex(ClampGrid(cell + int3(x, y, z)))]) * weight;
            }
        }
    }

    densitySurface.DensityWrite[dispatchThreadID] = density / (65536.0 * 64.0);
}

[shader("compute")]
[numthreads(4, 4, 4)]
void BuildFieldCS(uint3 dispatchThreadID : SV_DispatchThreadID, uint3 groupID : SV_GroupID, uint groupIndex : SV_GroupIndex)
{
    int3 cell = int3(dispatchThreadID);
    float3 gradient = float3(ReadDensity(cell + int3(1, 0, 0)) - ReadDensity(cell - int3(1, 0, 0)),
                            ReadDensity(cell + int3(0, 1, 0)) - ReadDensity(cell - int3(0, 1, 0)),
                            ReadDensity(cell + int3(0, 0, 1)) - ReadDensity(cell - int3(0, 0, 1))) * (0.5 * densitySurface.InverseCellSize);
    densitySurface.FieldWrite[dispatchThreadID] = float4(-gradient, ReadDensity(cell));

    float maximum = 0.0;

    for (int z = 0; z < 2; z++)
    {
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                maximum = max(maximum, ReadDensity(cell + int3(x, y, z)));
            }
        }
    }

    occupancyValues[groupIndex] = maximum;
    GroupMemoryBarrierWithGroupSync();

    for (uint stride = 32; stride > 0; stride >>= 1)
    {
        if (groupIndex < stride)
        {
            occupancyValues[groupIndex] = max(occupancyValues[groupIndex], occupancyValues[groupIndex + stride]);
        }

        GroupMemoryBarrierWithGroupSync();
    }

    if (groupIndex == 0)
    {
        densitySurface.OccupancyWrite[groupID] = occupancyValues[0];
    }
}

[shader("compute")]
[numthreads(8, 8, 1)]
void TraceCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;

    if (any(pixel >= densitySurface.ScreenDimensions))
    {
        return;
    }

    densitySurface.DepthOutput[pixel] = 0.0;
    densitySurface.ThicknessOutput[pixel] = 0.0;
    densitySurface.NormalOutput[pixel] = float4(0.0);

    float2 uv = (float2(pixel) + 0.5) / float2(densitySurface.ScreenDimensions);
    float3 viewDirection = SurfaceViewRay(uv, densitySurface.InvProjection);
    float3 origin = mul(float4(0.0, 0.0, 0.0, 1.0), densitySurface.InvView).xyz;
    float3 direction = normalize(mul(float4(viewDirection, 0.0), densitySurface.InvView).xyz);
    float nearDistance;
    float farDistance;
    float3 entryNormal;

    if (!IntersectTank(origin, direction, nearDistance, farDistance, entryNormal))
    {
        return;
    }

    uint sceneWidth;
    uint sceneHeight;
    densitySurface.SceneDepth.GetDimensions(sceneWidth, sceneHeight);
    float sceneDepth = densitySurface.SceneDepth.Load(int3(int2(uv * float2(sceneWidth, sceneHeight)), 0));

    if (sceneDepth > 0.0)
    {
        farDistance = min(farDistance, sceneDepth / -viewDirection.z);
    }

    float distance = max(nearDistance, 0.0);

    if (distance >= farDistance)
    {
        return;
    }

    bool inside = SampleField(origin + direction * distance).w >= densitySurface.IsoValue;
    bool found = inside && nearDistance >= 0.0;
    float firstDistance = distance;
    float3 firstNormal = entryNormal;
    float segmentStart = distance;
    float thickness = 0.0;
    float stepSize = densitySurface.CellSize * 0.5;

    while (distance < farDistance)
    {
        float3 position = origin + direction * distance;
        float nextDistance = min(distance + stepSize, farDistance);

        if (!inside)
        {
            int3 brick = OccupancyBrick(position, direction);

            if (densitySurface.OccupancyRead.Load(int4(brick, 0)) < densitySurface.IsoValue)
            {
                nextDistance = min(BrickExitDistance(origin, direction, brick), farDistance);

                if (nextDistance <= distance)
                {
                    nextDistance = min(distance + stepSize, farDistance);
                }
            }
        }

        bool nextInside = SampleField(origin + direction * nextDistance).w >= densitySurface.IsoValue;

        if (inside != nextInside)
        {
            float intersection = RefineIntersection(origin, direction, distance, nextDistance, inside);

            if (!found)
            {
                found = true;
                firstDistance = intersection;
                float3 normal = SampleField(origin + direction * intersection).xyz;
                float normalLengthSquared = dot(normal, normal);
                firstNormal = normalLengthSquared > 0.0 ? normal * rsqrt(normalLengthSquared) : -direction;
            }

            if (nextInside)
            {
                segmentStart = intersection;
            }
            else
            {
                thickness += intersection - segmentStart;
            }
        }

        inside = nextInside;
        distance = nextDistance;
    }

    if (inside)
    {
        thickness += farDistance - segmentStart;
    }

    if (found)
    {
        float3 position = origin + direction * firstDistance;
        densitySurface.DepthOutput[pixel] = -mul(float4(position, 1.0), densitySurface.View).z;
        densitySurface.ThicknessOutput[pixel] = thickness;
        densitySurface.NormalOutput[pixel] = float4(firstNormal, 1.0);
    }
}
