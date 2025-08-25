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
                Vector3 normal = tri.normal;
                Vector3 viewDir = -currentRay.direction;

                // Direct lighting
                Color direct = SampleLights(tri.material, normal, viewDir, hitPos, nodes, triangles, lights);
                radiance += throughput * direct;

                // Russian roulette termination
                if (depth >= RussianRouletteDepth)
                {
                    float q = Mathf.Max(throughput.r, Mathf.Max(throughput.g, throughput.b));
                    q = Mathf.Clamp01(q);
                    if (Random.value > q)
                        break;
                    throughput /= Mathf.Max(q, 1e-3f);
                }

                // Sample next direction from BRDF
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
                        if (Raycaster.Raycast(shadowRay, nodes, triangles, out float shadowDist, out _) &&
                            shadowDist < lightDistance)
                            continue;

                        Color brdf = EvaluateBrdf(mat, normal, lightDir, viewDir);
                        result += brdf * light.color * light.intensity;
                        break;
                    }
                    case LightType.Directional:
                    {
                        Vector3 lightDir = -light.direction.normalized;
                        Ray shadowRay = new Ray(hitPos + lightDir * 1e-3f, lightDir);
                        if (Raycaster.Raycast(shadowRay, nodes, triangles, out _, out _))
                            continue;

                        Color brdf = EvaluateBrdf(mat, normal, lightDir, viewDir);
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
                        if (Raycaster.Raycast(shadowRay, nodes, triangles, out float shadowDist, out _) &&
                            shadowDist < lightDistance)
                            continue;

                        Color brdf = EvaluateBrdf(mat, normal, lightDir, viewDir);
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
                            Vector3 samplePos = light.position +
                                (Random.value - 0.5f) * light.size.x * right +
                                (Random.value - 0.5f) * light.size.y * up;
                            Vector3 toLight = samplePos - hitPos;
                            float lightDistance = toLight.magnitude;
                            Vector3 lightDir = toLight / lightDistance;

                            Ray shadowRay = new Ray(hitPos + lightDir * 1e-3f, lightDir);
                            if (Raycaster.Raycast(shadowRay, nodes, triangles, out float shadowDist, out _) &&
                                shadowDist < lightDistance)
                                continue;

                            Color brdf = EvaluateBrdf(mat, normal, lightDir, viewDir);
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
                // Perfect mirror reflection
                Vector3 dir = Vector3.Reflect(-viewDir, normal).normalized;
                brdf = Color.white * metallic;
                pdf = Mathf.Max(metallic, 1e-3f);
                return dir;
            }
            else
            {
                // Cosine-weighted diffuse reflection
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

            // Build orthonormal basis
            Vector3 tangent = Vector3.Normalize(Vector3.Cross(normal, Mathf.Abs(normal.x) > 0.1f ? Vector3.up : Vector3.right));
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            return (x * tangent + y * bitangent + z * normal).normalized;
        }

        static Color EvaluateBrdf(LilToonParameters mat, Vector3 normal, Vector3 lightDir, Vector3 viewDir)
        {
            float ndotl = Mathf.Max(0f, Vector3.Dot(normal, lightDir));
            Color diffuse = mat.color * ndotl * (1f - mat.metallic);

            Vector3 halfDir = (lightDir + viewDir).normalized;
            float ndoth = Mathf.Max(0f, Vector3.Dot(normal, halfDir));
            float shininess = Mathf.Lerp(1f, 256f, 1f - mat.roughness);
            Color spec = Color.white * mat.metallic * Mathf.Pow(ndoth, shininess);

            return diffuse + spec;
        }
    }
}

