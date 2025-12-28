using System.Numerics;
using Zenith.NET;

namespace SponzaScene.Models;

internal readonly record struct Node(string Name, Matrix4x4 WorldMatrix, uint VertexCount, IndirectDrawIndexedArgs Args, uint Material);