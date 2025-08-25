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
            public Vector3 n0;
            public Vector3 n1;
            public Vector3 n2;
            public Vector4 t0;
            public Vector4 t1;
            public Vector4 t2;
            public Vector2 uv0;
            public Vector2 uv1;
            public Vector2 uv2;
            public int materialIndex;
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
        public static List<BvhNode> Build(List<GeometryCollector.MeshData> meshes, out List<Triangle> triangles, out List<LilToonParameters> materials)
        {
            triangles = new List<Triangle>();
            materials = new List<LilToonParameters>();
            foreach (var mesh in meshes)
            {
                int matIndex = materials.Count;
                materials.Add(mesh.material);
                triangles.AddRange(TrianglesFromMesh(mesh, matIndex));
            }

            var nodes = new List<BvhNode>();
            if (triangles.Count > 0)
            {
                BuildRecursive(triangles, 0, triangles.Count, nodes);
            }
            return nodes;
        }

        static int BuildRecursive(List<Triangle> triangles, int start, int count, List<BvhNode> nodes)
        {
            if (count <= 0)
                return -1;

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
            var leftBounds = new Bounds[count];
            var rightBounds = new Bounds[count];
            for (int axis = 0; axis < 3; ++axis)
            {
                triangles.Sort(start, count, new TriangleComparer(axis));

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

        public static List<Triangle> TrianglesFromMesh(GeometryCollector.MeshData mesh, int matIndex)
        {
            var tris = new List<Triangle>();
            var verts = mesh.vertices;
            var norms = mesh.normals;
            var tans = mesh.tangents;
            var uvs = mesh.uvs;
            var indices = mesh.indices;
            Matrix4x4 m = mesh.localToWorld;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int i0 = indices[i];
                int i1 = indices[i + 1];
                int i2 = indices[i + 2];
                Vector3 v0 = m.MultiplyPoint3x4(verts[i0]);
                Vector3 v1 = m.MultiplyPoint3x4(verts[i1]);
                Vector3 v2 = m.MultiplyPoint3x4(verts[i2]);

                Vector3 n0 = m.MultiplyVector(norms[i0]).normalized;
                Vector3 n1 = m.MultiplyVector(norms[i1]).normalized;
                Vector3 n2 = m.MultiplyVector(norms[i2]).normalized;

                Vector4 tan0 = tans[i0];
                Vector4 tan1 = tans[i1];
                Vector4 tan2 = tans[i2];

                Vector3 t0w = m.MultiplyVector(new Vector3(tan0.x, tan0.y, tan0.z)).normalized;
                Vector3 t1w = m.MultiplyVector(new Vector3(tan1.x, tan1.y, tan1.z)).normalized;
                Vector3 t2w = m.MultiplyVector(new Vector3(tan2.x, tan2.y, tan2.z)).normalized;

                Triangle t = new Triangle
                {
                    v0 = v0,
                    v1 = v1,
                    v2 = v2,
                    n0 = n0,
                    n1 = n1,
                    n2 = n2,
                    t0 = new Vector4(t0w.x, t0w.y, t0w.z, tan0.w),
                    t1 = new Vector4(t1w.x, t1w.y, t1w.z, tan1.w),
                    t2 = new Vector4(t2w.x, t2w.y, t2w.z, tan2.w),
                    uv0 = uvs[i0],
                    uv1 = uvs[i1],
                    uv2 = uvs[i2],
                    materialIndex = matIndex
                };
                tris.Add(t);
            }
            return tris;
        }
    }
}

