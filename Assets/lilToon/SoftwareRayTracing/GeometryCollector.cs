using System.Collections.Generic;
using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Collects scene geometry (triangles, normals, UVs) from Unity components.
    /// </summary>
    public static class GeometryCollector
    {
        static Vector3[] CalculateNormals(Vector3[] verts, int[] indices)
        {
            var norms = new Vector3[verts.Length];
            for (int i = 0; i < indices.Length; i += 3)
            {
                int i0 = indices[i];
                int i1 = indices[i + 1];
                int i2 = indices[i + 2];
                Vector3 v0 = verts[i0];
                Vector3 v1 = verts[i1];
                Vector3 v2 = verts[i2];
                Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
                norms[i0] += n;
                norms[i1] += n;
                norms[i2] += n;
            }
            for (int i = 0; i < norms.Length; ++i)
                norms[i] = norms[i].normalized;
            return norms;
        }

        static Vector4[] CalculateTangents(Vector3[] verts, Vector2[] uvs, int[] indices, Vector3[] norms)
        {
            var tan1 = new Vector3[verts.Length];
            var tan2 = new Vector3[verts.Length];
            for (int i = 0; i < indices.Length; i += 3)
            {
                int i0 = indices[i];
                int i1 = indices[i + 1];
                int i2 = indices[i + 2];
                Vector3 v0 = verts[i0];
                Vector3 v1 = verts[i1];
                Vector3 v2 = verts[i2];
                Vector2 w0 = uvs[i0];
                Vector2 w1 = uvs[i1];
                Vector2 w2 = uvs[i2];

                float x1 = v1.x - v0.x; float x2 = v2.x - v0.x;
                float y1 = v1.y - v0.y; float y2 = v2.y - v0.y;
                float z1 = v1.z - v0.z; float z2 = v2.z - v0.z;

                float s1 = w1.x - w0.x; float s2 = w2.x - w0.x;
                float t1 = w1.y - w0.y; float t2 = w2.y - w0.y;

                float r = 1.0f / (s1 * t2 - s2 * t1 + 1e-8f);
                Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);
                tan1[i0] += sdir; tan1[i1] += sdir; tan1[i2] += sdir;
                tan2[i0] += tdir; tan2[i1] += tdir; tan2[i2] += tdir;
            }

            var tangents = new Vector4[verts.Length];
            for (int i = 0; i < verts.Length; ++i)
            {
                Vector3 n = norms[i];
                Vector3 t = tan1[i];
                Vector3 tangent = (t - n * Vector3.Dot(n, t)).normalized;
                float w = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0.0f) ? -1.0f : 1.0f;
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, w);
            }
            return tangents;
        }

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
                var mats = renderer ? renderer.sharedMaterials : null;

                var verts = mesh.vertices;
                // Use all triangles for normal/tangent calculation
                var allIndices = mesh.triangles;
                var norms = mesh.normals;
                if(norms == null || norms.Length != verts.Length)
                    norms = CalculateNormals(verts, allIndices);

                Vector2[] uvs = mesh.uv;
                if(uvs == null || uvs.Length != verts.Length)
                    uvs = new Vector2[verts.Length];

                Vector4[] tans = mesh.tangents;
                if(tans == null || tans.Length != verts.Length)
                    tans = CalculateTangents(verts, uvs, allIndices, norms);

                int subMeshCount = mesh.subMeshCount;
                for(int sm = 0; sm < subMeshCount; sm++)
                {
                    var indices = mesh.GetTriangles(sm);
                    var mat = (mats != null && sm < mats.Length)
                        ? ParameterExtractor.FromMaterial(mats[sm])
                        : new LilToonParameters();

                    result.Add(new MeshData{
                        vertices = verts,
                        normals = norms,
                        uvs = uvs,
                        tangents = tans,
                        indices = indices,
                        material = mat,
                        localToWorld = mf.transform.localToWorldMatrix
                    });
                }
            }

            Mesh bakeMesh = null;
            foreach(var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(bakeMesh == null)
                    bakeMesh = new Mesh();
                else
                    bakeMesh.Clear();
                smr.BakeMesh(bakeMesh);
                var mats = smr.sharedMaterials;

                var verts = bakeMesh.vertices;
                var allIndices = bakeMesh.triangles;
                var norms = bakeMesh.normals;
                if(norms == null || norms.Length != verts.Length)
                    norms = CalculateNormals(verts, allIndices);

                var uvs = bakeMesh.uv;
                if(uvs == null || uvs.Length != verts.Length)
                    uvs = new Vector2[verts.Length];

                var tans = bakeMesh.tangents;
                if(tans == null || tans.Length != verts.Length)
                    tans = CalculateTangents(verts, uvs, allIndices, norms);

                int subMeshCount = bakeMesh.subMeshCount;
                for(int sm = 0; sm < subMeshCount; sm++)
                {
                    var indices = bakeMesh.GetTriangles(sm);
                    var mat = (mats != null && sm < mats.Length)
                        ? ParameterExtractor.FromMaterial(mats[sm])
                        : new LilToonParameters();

                    result.Add(new MeshData{
                        vertices = verts,
                        normals = norms,
                        uvs = uvs,
                        tangents = tans,
                        indices = indices,
                        material = mat,
                        localToWorld = smr.transform.localToWorldMatrix
                    });
                }
            }

            if(bakeMesh != null)
                Object.Destroy(bakeMesh);

            return result;
        }
    }
}
