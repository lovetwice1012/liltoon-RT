using System.Collections.Generic;
using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Path tracing shader evaluation with Monte Carlo integration.
    /// Samples the BRDF at each bounce, accumulates radiance and
    /// terminates paths using Russian roulette.
    /// </summary>
    public static class Shading
    {
        const int AreaLightSamples = 4;
        const int MaxDepth = 8;
        const int RussianRouletteDepth = 3;

        /// <summary>
        /// Evaluate the incoming radiance along a ray using Monte Carlo
        /// path tracing. Lights are sampled explicitly and indirect
        /// illumination is gathered by recursively sampling the BRDF.
        /// </summary>
        public static Color Shade(
            Ray ray,
            List<BvhBuilder.BvhNode> nodes,
            List<BvhBuilder.Triangle> triangles,
            List<LightCollector.LightData> lights)
        {
            Color radiance = Color.black;
            Color throughput = Color.white;
            Ray currentRay = ray;

            for (int depth = 0; depth < MaxDepth; depth++)
            {
                if (!Raycaster.Raycast(currentRay, nodes, triangles, out float dist, out int triIndex))
                    break;

                var tri = triangles[triIndex];
                Vector3 hitPos = currentRay.origin + currentRay.direction * dist;

                Vector3 bary = Barycentric(hitPos, tri.v0, tri.v1, tri.v2);
                Vector2 uv = tri.uv0 * bary.x + tri.uv1 * bary.y + tri.uv2 * bary.z;
                Vector3 normal = (tri.n0 * bary.x + tri.n1 * bary.y + tri.n2 * bary.z).normalized;
                Vector4 tan = tri.t0 * bary.x + tri.t1 * bary.y + tri.t2 * bary.z;
                Vector3 tangent = new Vector3(tan.x, tan.y, tan.z).normalized;
                Vector3 bitangent = Vector3.Cross(normal, tangent) * tan.w;

                if (tri.material.normalMap != null)
                {
                    Color ncol = tri.material.normalMap.GetPixelBilinear(uv.x, uv.y);
                    Vector3 nTangent = new Vector3(ncol.r * 2f - 1f, ncol.g * 2f - 1f, ncol.b * 2f - 1f);
                    normal = (tangent * nTangent.x + bitangent * nTangent.y + normal * nTangent.z).normalized;
                }

                Color albedo = tri.material.color;
                if (tri.material.albedoMap != null)
                    albedo *= tri.material.albedoMap.GetPixelBilinear(uv.x, uv.y);

                Vector3 viewDir = -currentRay.direction;
                Color direct = SampleLights(albedo, tri.material, normal, viewDir, hitPos, nodes, triangles, lights);
                radiance += throughput * direct;

                if (depth >= RussianRouletteDepth)
                {
                    float q = Mathf.Max(throughput.r, Mathf.Max(throughput.g, throughput.b));
                    q = Mathf.Clamp01(q);
                    if (Random.value > q)
                        break;
                    throughput /= Mathf.Max(q, 1e-3f);
                }

                Vector3 newDir = SampleBrdf(tri.material, normal, viewDir, out Color brdf, out float pdf);
                float ndotd = Mathf.Max(0f, Vector3.Dot(newDir, normal));
                if (pdf <= 0f || ndotd <= 0f)
                    break;

                throughput *= brdf * ndotd / pdf;
                currentRay = new Ray(hitPos + newDir * 1e-3f, newDir);
            }

            return radiance;
        }

        static Color SampleLights(
            Color albedo,
            LilToonParameters mat,
            Vector3 normal,
            Vector3 viewDir,
            Vector3 hitPos,
            List<BvhBuilder.BvhNode> nodes,
            List<BvhBuilder.Triangle> triangles,
            List<LightCollector.LightData> lights)
        {
            Color result = Color.black;

            foreach (var light in lights)
            {
                switch (light.type)
                {
                    case LightType.Point:
                    {
                        Vector3 toLight = light.position - hitPos;
                        float lightDistance = toLight.magnitude;
                        Vector3 lightDir = toLight / lightDistance;

                        Ray shadowRay = new Ray(hitPos + lightDir * 1e-3f, lightDir);
                        if (Raycaster.Raycast(shadowRay, nodes, triangles, out float shadowDist, out _) && shadowDist < lightDistance)
                            continue;

                        Color brdf = EvaluateBrdf(albedo, mat.metallic, mat.roughness, normal, lightDir, viewDir);
                        result += brdf * light.color * light.intensity;
                        break;
                    }
                    case LightType.Directional:
                    {
                        Vector3 lightDir = -light.direction.normalized;
                        Ray shadowRay = new Ray(hitPos + lightDir * 1e-3f, lightDir);
                        if (Raycaster.Raycast(shadowRay, nodes, triangles, out _, out _))
                            continue;
                        Color brdf = EvaluateBrdf(albedo, mat.metallic, mat.roughness, normal, lightDir, viewDir);
                        result += brdf * light.color * light.intensity;
                        break;
                    }
                    case LightType.Spot:
                    {
                        Vector3 toLight = light.position - hitPos;
                        float lightDistance = toLight.magnitude;
                        Vector3 lightDir = toLight / lightDistance;

                        float cosAngle = Vector3.Dot(lightDir, light.direction.normalized);
                        float cutoff = Mathf.Cos(light.angle * 0.5f * Mathf.Deg2Rad);
                        if (cosAngle < cutoff)
                            continue;

                        Ray shadowRay = new Ray(hitPos + lightDir * 1e-3f, lightDir);
                        if (Raycaster.Raycast(shadowRay, nodes, triangles, out float shadowDist, out _) && shadowDist < lightDistance)
                            continue;

                        Color brdf = EvaluateBrdf(albedo, mat.metallic, mat.roughness, normal, lightDir, viewDir);
                        result += brdf * light.color * light.intensity * cosAngle;
                        break;
                    }
                    case LightType.Area:
                    {
                        Vector3 right = Vector3.Cross(light.direction, light.up).normalized;
                        Vector3 up = light.up.normalized;
                        Color contrib = Color.black;

                        for (int i = 0; i < AreaLightSamples; i++)
                        {
                            Vector3 samplePos =
                                light.position +
                                (Random.value - 0.5f) * light.size.x * right +
                                (Random.value - 0.5f) * light.size.y * up;
                            Vector3 toLight = samplePos - hitPos;
                            float lightDistance = toLight.magnitude;
                            Vector3 lightDir = toLight / lightDistance;

                            Ray shadowRay = new Ray(hitPos + lightDir * 1e-3f, lightDir);
                            if (Raycaster.Raycast(shadowRay, nodes, triangles, out float shadowDist, out _) && shadowDist < lightDistance)
                                continue;

                            Color brdf = EvaluateBrdf(albedo, mat.metallic, mat.roughness, normal, lightDir, viewDir);
                            contrib += brdf * light.color * light.intensity;
                        }

                        result += contrib / AreaLightSamples;
                        break;
                    }
                }
            }

            return result;
        }

        static Vector3 SampleBrdf(
            LilToonParameters mat,
            Vector3 normal,
            Vector3 viewDir,
            out Color brdf,
            out float pdf)
        {
            float metallic = mat.metallic;
            if (Random.value < metallic)
            {
                Vector3 dir = Vector3.Reflect(-viewDir, normal).normalized;
                brdf = Color.white * metallic;
                pdf = Mathf.Max(metallic, 1e-3f);
                return dir;
            }
            else
            {
                Vector3 dir = SampleHemisphere(normal);
                float cos = Mathf.Max(0f, Vector3.Dot(dir, normal));
                brdf = mat.color * (1f - metallic) / Mathf.PI;
                pdf = cos * (1f - metallic) / Mathf.PI;
                return dir;
            }
        }

        static Vector3 SampleHemisphere(Vector3 normal)
        {
            float u1 = Random.value;
            float u2 = Random.value;
            float r = Mathf.Sqrt(u1);
            float theta = 2f * Mathf.PI * u2;
            float x = r * Mathf.Cos(theta);
            float y = r * Mathf.Sin(theta);
            float z = Mathf.Sqrt(1f - u1);

            Vector3 tangent = Vector3.Normalize(Vector3.Cross(normal, Mathf.Abs(normal.x) > 0.1f ? Vector3.up : Vector3.right));
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            return (x * tangent + y * bitangent + z * normal).normalized;
        }

        static Color EvaluateBrdf(Color albedo, float metallic, float roughness, Vector3 normal, Vector3 lightDir, Vector3 viewDir)
        {
            Vector3 halfDir = (lightDir + viewDir).normalized;

            float ndotl = Mathf.Max(0f, Vector3.Dot(normal, lightDir));
            float ndotv = Mathf.Max(0f, Vector3.Dot(normal, viewDir));
            float ndoth = Mathf.Max(0f, Vector3.Dot(normal, halfDir));
            float vdoth = Mathf.Max(0f, Vector3.Dot(viewDir, halfDir));

            float a = roughness * roughness;
            float a2 = a * a;
            float denom = ndoth * ndoth * (a2 - 1f) + 1f;
            float D = a2 / (Mathf.PI * denom * denom + 1e-7f);

            float k = (roughness + 1f);
            k = (k * k) / 8f;
            float Gv = ndotv / (ndotv * (1f - k) + k);
            float Gl = ndotl / (ndotl * (1f - k) + k);
            float G = Gv * Gl;

            Color F0 = Color.Lerp(new Color(0.04f, 0.04f, 0.04f, 1f), albedo, metallic);
            Color F = F0 + (Color.white - F0) * Mathf.Pow(1f - vdoth, 5f);

            Color spec = F * (D * G / (4f * ndotv * ndotl + 1e-5f));
            Color diffuse = (albedo / Mathf.PI) * (1f - metallic);

            return (diffuse + spec) * ndotl;
        }

        static Vector3 Barycentric(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 v0 = b - a, v1 = c - a, v2 = p - a;
            float d00 = Vector3.Dot(v0, v0);
            float d01 = Vector3.Dot(v0, v1);
            float d11 = Vector3.Dot(v1, v1);
            float d20 = Vector3.Dot(v2, v0);
            float d21 = Vector3.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1f - v - w;
            return new Vector3(u, v, w);
        }
    }
}

