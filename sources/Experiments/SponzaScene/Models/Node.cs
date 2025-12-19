using Zenith.NET;

namespace SponzaScene.Models;

internal readonly record struct Node(string Name, uint VertexCount, IndirectDrawIndexedArgs Args, uint Material);