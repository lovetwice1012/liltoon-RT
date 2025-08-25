using System.Collections.Generic;
using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Casts rays against the BVH and triangle data.
    /// </summary>
    public static class Raycaster
    {
        /// <summary>
        /// Traverse the BVH and find the closest intersecting triangle.
        /// </summary>
        public static bool Raycast(Ray ray, List<BvhBuilder.BvhNode> nodes, List<BvhBuilder.Triangle> triangles, out float distance, out int triangleIndex)
        {
            distance = float.MaxValue;
            triangleIndex = -1;
            return Traverse(0, ray, nodes, triangles, ref distance, ref triangleIndex);
        }

        static bool Traverse(int nodeIndex, Ray ray, List<BvhBuilder.BvhNode> nodes, List<BvhBuilder.Triangle> triangles, ref float hitDist, ref int hitTri)
        {
            var node = nodes[nodeIndex];
            if (!RayAabb(ray, node.bounds, hitDist))
                return false;

            bool hit = false;
            if (node.left == -1 && node.right == -1)
            {
                for (int i = node.start; i < node.start + node.count; ++i)
                {
                    if (RayTriangle(ray, triangles[i], out float dist) && dist < hitDist)
                    {
                        hit = true;
                        hitDist = dist;
                        hitTri = i;
                    }
                }
            }
            else
            {
                if (node.left != -1)
                    hit |= Traverse(node.left, ray, nodes, triangles, ref hitDist, ref hitTri);
                if (node.right != -1)
                    hit |= Traverse(node.right, ray, nodes, triangles, ref hitDist, ref hitTri);
            }
            return hit;
        }

        static bool RayAabb(Ray ray, Bounds bounds, float maxDist)
        {
            float tmin = 0f;
            float tmax = maxDist;
            Vector3 o = ray.origin;
            Vector3 d = ray.direction;

            for (int i = 0; i < 3; i++)
            {
                float dir = d[i];
                float min = bounds.min[i];
                float max = bounds.max[i];
                if (Mathf.Abs(dir) < 1e-8f)
                {
                    if (o[i] < min || o[i] > max)
                        return false;
                }
                else
                {
                    float inv = 1f / dir;
                    float t1 = (min - o[i]) * inv;
                    float t2 = (max - o[i]) * inv;
                    if (t1 > t2)
                    {
                        float tmp = t1;
                        t1 = t2;
                        t2 = tmp;
                    }
                    tmin = Mathf.Max(tmin, t1);
                    tmax = Mathf.Min(tmax, t2);
                    if (tmin > tmax)
                        return false;
                }
            }

            return tmin < maxDist;
        }

        static bool RayTriangle(Ray ray, BvhBuilder.Triangle tri, out float distance)
        {
            distance = 0f;
            Vector3 edge1 = tri.v1 - tri.v0;
            Vector3 edge2 = tri.v2 - tri.v0;
            Vector3 pvec = Vector3.Cross(ray.direction, edge2);
            float det = Vector3.Dot(edge1, pvec);
            if (Mathf.Abs(det) < 1e-8f)
                return false;
            float invDet = 1f / det;
            Vector3 tvec = ray.origin - tri.v0;
            float u = Vector3.Dot(tvec, pvec) * invDet;
            if (u < 0f || u > 1f)
                return false;
            Vector3 qvec = Vector3.Cross(tvec, edge1);
            float v = Vector3.Dot(ray.direction, qvec) * invDet;
            if (v < 0f || u + v > 1f)
                return false;
            distance = Vector3.Dot(edge2, qvec) * invDet;
            return distance > 0f;
        }
    }
}
