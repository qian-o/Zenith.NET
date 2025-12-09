using Zenith.NET;

namespace SponzaScene.Models;

internal class Node(string name, IndirectDrawIndexedArgs args, Material material)
{
    public string Name { get; } = name;

    public IndirectDrawIndexedArgs Args { get; } = args;

    public Material Material { get; } = material;
}
