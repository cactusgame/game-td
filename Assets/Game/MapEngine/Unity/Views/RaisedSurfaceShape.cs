using System.Collections.Generic;
using UnityEngine;

namespace Game.MapEngine.Unity.Views
{
    public sealed class RaisedSurfaceShape
    {
        public SurfaceMeshData Mesh;
        public Vector2[] GroundOutline;
        public readonly List<Vector2[]> Facades = new List<Vector2[]>();
    }
}
