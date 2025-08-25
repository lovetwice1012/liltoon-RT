using System.Collections.Generic;
using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Builds a simple bounding volume hierarchy (BVH) over triangles.
    /// </summary>
    public static class BvhBuilder
    {
        const int LeafThreshold = 4;
        public struct Triangle
        {
            public Vector3 v0;
            public Vector3 v1;
            public Vector3 v2;
            public Vector3 normal;
            public LilToonParameters material;
        }

        public struct BvhNode
        {
            public Bounds bounds;
            public int left;
            public int right;
            public int start;
            public int count;
        }

        /// <summary>
        /// Builds a BVH from mesh data collected via <see cref="GeometryCollector"/>.
        /// </summary>
        public static List<BvhNode> Build(List<GeometryCollector.MeshData> meshes, out List<Triangle> triangles)
        {
            triangles = new List<Triangle>();
            foreach (var mesh in meshes)
            {
                triangles.AddRange(TrianglesFromMesh(mesh));
            }

            var nodes = new List<BvhNode>();
            BuildRecursive(triangles, 0, triangles.Count, nodes);
            return nodes;
        }

        static int BuildRecursive(List<Triangle> triangles, int start, int count, List<BvhNode> nodes)
        {
            Bounds bounds = new Bounds(triangles[start].v0, Vector3.zero);
            for (int i = start; i < start + count; ++i)
            {
                bounds.Encapsulate(triangles[i].v0);
                bounds.Encapsulate(triangles[i].v1);
                bounds.Encapsulate(triangles[i].v2);
            }

            var node = new BvhNode { bounds = bounds, start = start, count = count, left = -1, right = -1 };
            int nodeIndex = nodes.Count;
            nodes.Add(node);

            if (count <= LeafThreshold)
            {
                return nodeIndex;
            }

            float bestCost = float.MaxValue;
            int bestAxis = -1;
            int bestSplit = -1;

            for (int axis = 0; axis < 3; ++axis)
            {
                triangles.Sort(start, count, new TriangleComparer(axis));

                var leftBounds = new Bounds[count];
                var rightBounds = new Bounds[count];

                Bounds b = new Bounds(triangles[start].v0, Vector3.zero);
                for (int i = 0; i < count; ++i)
                {
                    int idx = start + i;
                    b.Encapsulate(triangles[idx].v0);
                    b.Encapsulate(triangles[idx].v1);
                    b.Encapsulate(triangles[idx].v2);
                    leftBounds[i] = b;
                }

                b = new Bounds(triangles[start + count - 1].v0, Vector3.zero);
                for (int i = count - 1; i >= 0; --i)
                {
                    int idx = start + i;
                    b.Encapsulate(triangles[idx].v0);
                    b.Encapsulate(triangles[idx].v1);
                    b.Encapsulate(triangles[idx].v2);
                    rightBounds[i] = b;
                }

                for (int i = 1; i < count; ++i)
                {
                    float cost = i * SurfaceArea(leftBounds[i - 1]) + (count - i) * SurfaceArea(rightBounds[i]);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestAxis = axis;
                        bestSplit = i;
                    }
                }
            }

            if (bestAxis == -1)
            {
                return nodeIndex;
            }

            triangles.Sort(start, count, new TriangleComparer(bestAxis));
            int mid = start + bestSplit;
            node.left = BuildRecursive(triangles, start, bestSplit, nodes);
            node.right = BuildRecursive(triangles, mid, count - bestSplit, nodes);
            nodes[nodeIndex] = node;
            return nodeIndex;
        }

        static float SurfaceArea(Bounds b)
        {
            Vector3 s = b.size;
            return 2f * (s.x * s.y + s.y * s.z + s.z * s.x);
        }

        class TriangleComparer : IComparer<Triangle>
        {
            int axis;
            public TriangleComparer(int axis) { this.axis = axis; }
            public int Compare(Triangle a, Triangle b)
            {
                float ac = (a.v0[axis] + a.v1[axis] + a.v2[axis]) / 3f;
                float bc = (b.v0[axis] + b.v1[axis] + b.v2[axis]) / 3f;
                return ac.CompareTo(bc);
            }
        }

        public static List<Triangle> TrianglesFromMesh(GeometryCollector.MeshData mesh)
        {
            var tris = new List<Triangle>();
            var verts = mesh.vertices;
            var indices = mesh.indices;
            Matrix4x4 m = mesh.localToWorld;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 v0 = m.MultiplyPoint3x4(verts[indices[i]]);
                Vector3 v1 = m.MultiplyPoint3x4(verts[indices[i + 1]]);
                Vector3 v2 = m.MultiplyPoint3x4(verts[indices[i + 2]]);
                Triangle t = new Triangle
                {
                    v0 = v0,
                    v1 = v1,
                    v2 = v2,
                    normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0)),
                    material = mesh.material
                };
                tris.Add(t);
            }
            return tris;
        }
    }
}

