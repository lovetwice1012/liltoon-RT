using System.Collections.Generic;
using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Builds a simple bounding volume hierarchy (BVH) over triangles.
    /// </summary>
    public static class BvhBuilder
    {
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

            if (count <= 2)
            {
                return nodeIndex;
            }

            Vector3 size = bounds.size;
            int axis = 0;
            if (size.y > size.x && size.y > size.z) axis = 1;
            else if (size.z > size.x && size.z > size.y) axis = 2;

            triangles.Sort(start, count, new TriangleComparer(axis));

            int mid = start + count / 2;
            node.left = BuildRecursive(triangles, start, mid - start, nodes);
            node.right = BuildRecursive(triangles, mid, start + count - mid, nodes);
            nodes[nodeIndex] = node;
            return nodeIndex;
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
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 v0 = verts[indices[i]];
                Vector3 v1 = verts[indices[i + 1]];
                Vector3 v2 = verts[indices[i + 2]];
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

