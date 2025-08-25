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

                var verts = mesh.vertices;
                var norms = mesh.normals;
                Vector4[] tans = mesh.tangents;
                Vector2[] uvs = mesh.uv;

                // Avoid mutating shared mesh assets by operating on a temporary copy
                Mesh temp = null;
                if(norms == null || norms.Length != verts.Length)
                {
                    temp = Object.Instantiate(mesh);
                    temp.RecalculateNormals();
                    norms = temp.normals;
                }

                if(uvs == null || uvs.Length != verts.Length)
                    uvs = new Vector2[verts.Length];

                if(tans == null || tans.Length != verts.Length)
                {
                    if(temp == null) temp = Object.Instantiate(mesh);
                    if(uvs.Length > 0)
                        temp.RecalculateTangents();
                    tans = temp.tangents;
                    if(tans == null || tans.Length != verts.Length)
                        tans = new Vector4[verts.Length];
                }

                if(temp != null)
                    Object.Destroy(temp);

                result.Add(new MeshData{
                    vertices = verts,
                    normals = norms,
                    uvs = uvs,
                    tangents = tans,
                    indices = mesh.triangles,
                    material = mat,
                    localToWorld = mf.transform.localToWorldMatrix
                });
            }

            Mesh bakeMesh = null;
            foreach(var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(bakeMesh == null)
                    bakeMesh = new Mesh();
                else
                    bakeMesh.Clear();
                smr.BakeMesh(bakeMesh);
                var mat = ParameterExtractor.FromMaterial(smr.sharedMaterial);

                var verts = bakeMesh.vertices;
                var norms = bakeMesh.normals;
                if(norms == null || norms.Length != verts.Length)
                {
                    bakeMesh.RecalculateNormals();
                    norms = bakeMesh.normals;
                }

                var uvs = bakeMesh.uv;
                if(uvs == null || uvs.Length != verts.Length)
                    uvs = new Vector2[verts.Length];

                var tans = bakeMesh.tangents;
                if(tans == null || tans.Length != verts.Length)
                {
                    if(uvs.Length > 0)
                        bakeMesh.RecalculateTangents();
                    tans = bakeMesh.tangents;
                    if(tans == null || tans.Length != verts.Length)
                        tans = new Vector4[verts.Length];
                }

                result.Add(new MeshData{
                    vertices = verts,
                    normals = norms,
                    uvs = uvs,
                    tangents = tans,
                    indices = bakeMesh.triangles,
                    material = mat,
                    localToWorld = smr.transform.localToWorldMatrix
                });
            }

            if(bakeMesh != null)
                Object.Destroy(bakeMesh);

            return result;
        }
    }
}
