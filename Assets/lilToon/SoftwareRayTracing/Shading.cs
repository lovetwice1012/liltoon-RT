using System.Collections.Generic;
using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Shading routine that fetches material parameters and computes
    /// direct lighting with shadows and simple reflections.
    /// </summary>
    public static class Shading
    {
        const int MaxDepth = 2;
        const int AreaLightSamples = 4;

        /// <summary>
        /// Shades a raycast hit using a simple BRDF and recursive reflections.
        /// </summary>
        public static Color Shade(Ray ray,
            List<BvhBuilder.BvhNode> nodes,
            List<BvhBuilder.Triangle> triangles,
            List<LightCollector.LightData> lights,
            int depth = 0)
        {
            if (depth > MaxDepth ||
                !Raycaster.Raycast(ray, nodes, triangles, out float dist, out int triIndex))
                return Color.black;

            var tri = triangles[triIndex];
            Vector3 hitPos = ray.origin + ray.direction * dist;
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

                        Color brdf = EvaluateBrdf(tri.material, tri.normal, lightDir, -ray.direction);
                        result += brdf * light.color * light.intensity;
                        break;
                    }
                    case LightType.Directional:
                    {
                        Vector3 lightDir = -light.direction.normalized;
                        Ray shadowRay = new Ray(hitPos + lightDir * 1e-3f, lightDir);
                        if (Raycaster.Raycast(shadowRay, nodes, triangles, out _, out _))
                            continue;

                        Color brdf = EvaluateBrdf(tri.material, tri.normal, lightDir, -ray.direction);
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

                        Color brdf = EvaluateBrdf(tri.material, tri.normal, lightDir, -ray.direction);
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

                            Color brdf = EvaluateBrdf(tri.material, tri.normal, lightDir, -ray.direction);
                            contrib += brdf * light.color * light.intensity;
                        }

                        result += contrib / AreaLightSamples;
                        break;
                    }
                }
            }

            if (depth < MaxDepth)
            {
                Vector3 reflectDir = Vector3.Reflect(ray.direction, tri.normal).normalized;
                Ray reflectRay = new Ray(hitPos + reflectDir * 1e-3f, reflectDir);
                Color reflected = Shade(reflectRay, nodes, triangles, lights, depth + 1);

                float fresnel = tri.material.metallic +
                                (1f - tri.material.metallic) *
                                Mathf.Pow(1f - Mathf.Max(0f, Vector3.Dot(-ray.direction, tri.normal)), 5f);
                float reflectivity = (1f - tri.material.roughness) * fresnel;
                result += reflected * reflectivity;
            }

            return result;
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
