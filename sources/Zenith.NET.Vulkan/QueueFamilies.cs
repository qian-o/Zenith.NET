using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal readonly unsafe struct QueueFamilies : IDisposable
{
    private readonly ZenithMarshal.Scope scope = new();

    public readonly (uint FamilyIndex, uint QueueIndex) Graphics;

    public readonly (uint FamilyIndex, uint QueueIndex) Compute;

    public readonly (uint FamilyIndex, uint QueueIndex) Transfer;

    public readonly (uint FamilyIndex, uint QueueIndex) Present;

    public readonly SharingMode SharingMode;

    public readonly uint IndexCount;

    public readonly uint* Indices;

    public QueueFamilies(VKGraphicsContext context, float* queuePriorities, out DeviceQueueCreateInfo[] createInfos)
    {
        uint familyCount = 0;
        context.Vk.GetPhysicalDeviceQueueFamilyProperties(context.PhysicalDevice, &familyCount, default);

        QueueFamilyProperties* families = stackalloc QueueFamilyProperties[(int)familyCount];
        context.Vk.GetPhysicalDeviceQueueFamilyProperties(context.PhysicalDevice, &familyCount, families);

        Family graphicsFamily = default;
        Family computeFamily = default;
        Family transferFamily = default;
        for (uint i = 0; i < familyCount; i++)
        {
            QueueFlags queueFlags = families[i].QueueFlags & ~(QueueFlags.SparseBindingBit | QueueFlags.ProtectedBit);
            uint queueCount = families[i].QueueCount;

            if (queueFlags.HasFlag(QueueFlags.GraphicsBit | QueueFlags.ComputeBit) && queueCount > graphicsFamily.QueueCount)
            {
                graphicsFamily = new(i, queueCount, 0);
            }
            else if (queueFlags.HasFlag(QueueFlags.ComputeBit) && !queueFlags.HasFlag(QueueFlags.GraphicsBit) && queueCount > computeFamily.QueueCount)
            {
                computeFamily = new(i, queueCount, 0);
            }
            else if (queueFlags is QueueFlags.TransferBit && queueCount > transferFamily.QueueCount)
            {
                transferFamily = new(i, queueCount, 0);
            }
        }

        Graphics = Acquire(ref graphicsFamily) ?? throw new NotSupportedException("This device does not have a queue family that supports both graphics and compute operations.");
        Present = Acquire(ref graphicsFamily) ?? Graphics;
        Compute = Acquire(ref computeFamily) ?? Acquire(ref graphicsFamily) ?? Graphics;
        Transfer = Acquire(ref transferFamily) ?? Acquire(ref computeFamily) ?? Acquire(ref graphicsFamily) ?? Graphics;

        createInfos = [.. new Family[] { graphicsFamily, computeFamily, transferFamily }.Where(static item => item.AcquiredCount is not 0).Select(item => new DeviceQueueCreateInfo()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = item.Index,
            QueueCount = item.AcquiredCount,
            PQueuePriorities = queuePriorities
        })];

        SharingMode = createInfos.Length is 1 ? SharingMode.Exclusive : SharingMode.Concurrent;
        IndexCount = (uint)createInfos.Length;
        Indices = (uint*)ZenithMarshal.AllocateAndFill(scope, [.. createInfos.Select(static item => item.QueueFamilyIndex)]);

        static (uint FamilyIndex, uint QueueIndex)? Acquire(ref Family family)
        {
            return family.AcquiredCount < family.QueueCount ? (family.Index, family.AcquiredCount++) : null;
        }
    }

    public void Dispose()
    {
        scope.Dispose();
    }
}

file struct Family(uint index, uint queueCount, uint acquiredCount)
{
    public uint Index = index;

    public uint QueueCount = queueCount;

    public uint AcquiredCount = acquiredCount;
}