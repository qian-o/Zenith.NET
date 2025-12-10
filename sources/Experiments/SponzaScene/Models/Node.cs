using Zenith.NET;

namespace SponzaScene.Models;

internal record struct Node(string Name, uint VertexCount, IndirectDrawIndexedArgs Args, uint Material);