using System.Collections.Generic;
using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Collects scene geometry (triangles, normals, UVs) from Unity components.
    /// </summary>
    public static class GeometryCollector
    {
        public struct MeshData
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uvs;
            public Vector4[] tangents;
            public int[] indices;
            public LilToonParameters material;
            public Matrix4x4 localToWorld;
        }

        /// <summary>
        /// Extracts mesh data from MeshFilter and SkinnedMeshRenderer under the given root.
        /// </summary>
        public static List<MeshData> Collect(GameObject root)
        {
            var result = new List<MeshData>();
            if(root == null) return result;

            foreach(var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = mf.sharedMesh;
                if(mesh == null) continue;
                var renderer = mf.GetComponent<Renderer>();
                var mat = renderer ? ParameterExtractor.FromMaterial(renderer.sharedMaterial) : new LilToonParameters();
                result.Add(new MeshData{
                    vertices = mesh.vertices,
                    normals = mesh.normals,
                    uvs = mesh.uv,
                    tangents = mesh.tangents,
                    indices = mesh.triangles,
                    material = mat,
                    localToWorld = mf.transform.localToWorldMatrix
                });
            }

            foreach(var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                smr.BakeMesh(mesh);
                var mat = ParameterExtractor.FromMaterial(smr.sharedMaterial);
                result.Add(new MeshData{
                    vertices = mesh.vertices,
                    normals = mesh.normals,
                    uvs = mesh.uv,
                    tangents = mesh.tangents,
                    indices = mesh.triangles,
                    material = mat,
                    localToWorld = smr.transform.localToWorldMatrix
                });
            }

            return result;
        }
    }
}
