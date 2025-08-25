using System.Collections.Generic;
using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Collects scene geometry (triangles, normals, UVs) from Unity components.
    /// </summary>
    public static class GeometryCollector
    {
        static Mesh _bakeMesh;
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
                if(norms == null || norms.Length != verts.Length)
                {
                    mesh.RecalculateNormals();
                    norms = mesh.normals;
                }

                var uvs = mesh.uv;
                if(uvs == null || uvs.Length != verts.Length)
                    uvs = new Vector2[verts.Length];

                var tans = mesh.tangents;
                if(tans == null || tans.Length != verts.Length)
                {
                    if(uvs.Length > 0)
                        mesh.RecalculateTangents();
                    tans = mesh.tangents;
                    if(tans == null || tans.Length != verts.Length)
                        tans = new Vector4[verts.Length];
                }

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

            foreach(var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(_bakeMesh == null)
                    _bakeMesh = new Mesh();
                else
                    _bakeMesh.Clear();
                smr.BakeMesh(_bakeMesh);
                var mat = ParameterExtractor.FromMaterial(smr.sharedMaterial);

                var verts = _bakeMesh.vertices;
                var norms = _bakeMesh.normals;
                if(norms == null || norms.Length != verts.Length)
                {
                    _bakeMesh.RecalculateNormals();
                    norms = _bakeMesh.normals;
                }

                var uvs = _bakeMesh.uv;
                if(uvs == null || uvs.Length != verts.Length)
                    uvs = new Vector2[verts.Length];

                var tans = _bakeMesh.tangents;
                if(tans == null || tans.Length != verts.Length)
                {
                    if(uvs.Length > 0)
                        _bakeMesh.RecalculateTangents();
                    tans = _bakeMesh.tangents;
                    if(tans == null || tans.Length != verts.Length)
                        tans = new Vector4[verts.Length];
                }

                result.Add(new MeshData{
                    vertices = verts,
                    normals = norms,
                    uvs = uvs,
                    tangents = tans,
                    indices = _bakeMesh.triangles,
                    material = mat,
                    localToWorld = smr.transform.localToWorldMatrix
                });
            }

            return result;
        }
    }
}
