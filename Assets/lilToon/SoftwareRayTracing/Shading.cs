using System;
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
        /// <summary>
        /// Evaluate the incoming radiance along a ray using Monte Carlo
        /// path tracing. Lights are sampled explicitly and indirect
        /// illumination is gathered by recursively sampling the BRDF.
        /// </summary>
        public static SpectralColor Shade(
            Ray ray,
            List<BvhBuilder.BvhNode> nodes,
            List<BvhBuilder.Triangle> triangles,
            List<LightCollector.LightData> lights,
            Color[] environment,
            int envWidth,
            int envHeight,
            int areaLightSamples,
            int maxDepth,
            int russianRouletteDepth,
            Random rng)
        {
            SpectralColor radiance = SpectralColor.Black;
            SpectralColor throughput = SpectralColor.White;
            Ray currentRay = ray;
            float prevPdf = 0f;

            for (int depth = 0; depth < maxDepth; depth++)
            {
                if (!Raycaster.Raycast(currentRay, nodes, triangles, out float dist, out int triIndex))
                {
                    if (environment != null)
                    {
                        SpectralColor env = SampleEnvironment(environment, envWidth, envHeight, currentRay.direction);
                        float envPdf = 1f / (4f * Mathf.PI);
                        float weight = prevPdf > 0f ? PowerHeuristic(prevPdf, envPdf) : 1f;
                        radiance += throughput * env * weight;
                    }
                    break;
                }

                var tri = triangles[triIndex];
                Vector3 hitPos = currentRay.origin + currentRay.direction * dist;

                Vector3 bary = Barycentric(hitPos, tri.v0, tri.v1, tri.v2);
                Vector2 uv = tri.uv0 * bary.x + tri.uv1 * bary.y + tri.uv2 * bary.z;
                Vector3 normal = (tri.n0 * bary.x + tri.n1 * bary.y + tri.n2 * bary.z).normalized;
                Vector4 tan = tri.t0 * bary.x + tri.t1 * bary.y + tri.t2 * bary.z;
                Vector3 tangent = new Vector3(tan.x, tan.y, tan.z).normalized;
                Vector3 bitangent = Vector3.Cross(normal, tangent) * tan.w;

                if (tri.material.normalPixels != null)
                {
                    Color ncol = SampleColor(tri.material.normalPixels, tri.material.normalWidth, tri.material.normalHeight, uv.x, uv.y);
                    Vector3 nTangent = new Vector3(ncol.r * 2f - 1f, ncol.g * 2f - 1f, ncol.b * 2f - 1f);
                    normal = (tangent * nTangent.x + bitangent * nTangent.y + normal * nTangent.z).normalized;
                }

                SpectralColor albedo = tri.material.color;
                if (tri.material.albedoPixels != null)
                    albedo *= SpectralColor.FromPixelData(tri.material.albedoPixels, tri.material.albedoWidth, tri.material.albedoHeight, uv.x, uv.y);

                Vector3 viewDir = -currentRay.direction;
                SpectralColor direct = SampleLights(albedo, tri.material, normal, viewDir, hitPos, nodes, triangles, lights, environment, envWidth, envHeight, areaLightSamples, rng);
                radiance += throughput * direct;

                if (depth >= russianRouletteDepth)
                {
                    float q = throughput.MaxComponent;
                    q = Mathf.Clamp01(q);
                    if (rng.NextDouble() > q)
                        break;
                    throughput /= Mathf.Max(q, 1e-3f);
                }

                Vector3 newDir = SampleBrdf(tri.material, normal, viewDir, rng, out SpectralColor brdf, out float pdf);
                float ndotd = Mathf.Max(0f, Vector3.Dot(newDir, normal));
                if (pdf <= 0f || ndotd <= 0f)
                    break;

                throughput *= brdf * (ndotd / pdf);
                currentRay = new Ray(hitPos + newDir * 1e-3f, newDir);
                prevPdf = pdf;
            }

            return radiance;
        }

        static SpectralColor SampleLights(
            SpectralColor albedo,
            LilToonParameters mat,
            Vector3 normal,
            Vector3 viewDir,
            Vector3 hitPos,
            List<BvhBuilder.BvhNode> nodes,
            List<BvhBuilder.Triangle> triangles,
            List<LightCollector.LightData> lights,
            Color[] environment,
            int envWidth,
            int envHeight,
            int areaLightSamples,
            Random rng)
        {
            SpectralColor result = SpectralColor.Black;

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

                        SpectralColor brdf = EvaluateBrdf(albedo, mat, normal, lightDir, viewDir);
                        float brdfPdf = PdfBrdf(mat, normal, viewDir, lightDir);
                        float lightPdf = 1f;
                        float weight = PowerHeuristic(lightPdf, brdfPdf);
                        result += brdf * light.spectrum * light.intensity * weight / Mathf.Max(lightPdf, 1e-3f);
                        break;
                    }
                    case LightType.Directional:
                    {
                        Vector3 lightDir = -light.direction.normalized;
                        Ray shadowRay = new Ray(hitPos + lightDir * 1e-3f, lightDir);
                        if (Raycaster.Raycast(shadowRay, nodes, triangles, out _, out _))
                            continue;
                        SpectralColor brdf = EvaluateBrdf(albedo, mat, normal, lightDir, viewDir);
                        float brdfPdf = PdfBrdf(mat, normal, viewDir, lightDir);
                        float lightPdf = 1f;
                        float weight = PowerHeuristic(lightPdf, brdfPdf);
                        result += brdf * light.spectrum * light.intensity * weight / Mathf.Max(lightPdf, 1e-3f);
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

                        SpectralColor brdf = EvaluateBrdf(albedo, mat, normal, lightDir, viewDir);
                        float brdfPdf = PdfBrdf(mat, normal, viewDir, lightDir);
                        float lightPdf = 1f;
                        float weight = PowerHeuristic(lightPdf, brdfPdf);
                        result += brdf * light.spectrum * light.intensity * cosAngle * weight / Mathf.Max(lightPdf, 1e-3f);
                        break;
                    }
                    case LightType.Area:
                    {
                        Vector3 right = Vector3.Cross(light.direction, light.up).normalized;
                        Vector3 up = light.up.normalized;
                        SpectralColor contrib = SpectralColor.Black;

                        for (int i = 0; i < areaLightSamples; i++)
                        {
                            Vector3 samplePos =
                                light.position +
                                ((float)rng.NextDouble() - 0.5f) * light.size.x * right +
                                ((float)rng.NextDouble() - 0.5f) * light.size.y * up;
                            Vector3 toLight = samplePos - hitPos;
                            float lightDistance = toLight.magnitude;
                            Vector3 lightDir = toLight / lightDistance;

                            Ray shadowRay = new Ray(hitPos + lightDir * 1e-3f, lightDir);
                            if (Raycaster.Raycast(shadowRay, nodes, triangles, out float shadowDist, out _) && shadowDist < lightDistance)
                                continue;

                            SpectralColor brdf = EvaluateBrdf(albedo, mat, normal, lightDir, viewDir);
                            float brdfPdf = PdfBrdf(mat, normal, viewDir, lightDir);
                            float lightPdf = 1f;
                            float weight = PowerHeuristic(lightPdf, brdfPdf);
                            contrib += brdf * light.spectrum * light.intensity * weight / Mathf.Max(lightPdf, 1e-3f);
                        }

                        result += contrib / Mathf.Max(1, areaLightSamples);
                        break;
                    }
                }
            }

            if (environment != null)
            {
                Vector3 lightDir = SampleSphere(rng);
                Ray shadowRay = new Ray(hitPos + lightDir * 1e-3f, lightDir);
                if (!Raycaster.Raycast(shadowRay, nodes, triangles, out _, out _))
                {
                    SpectralColor env = SampleEnvironment(environment, envWidth, envHeight, lightDir);
                    SpectralColor brdf = EvaluateBrdf(albedo, mat, normal, lightDir, viewDir);
                    float brdfPdf = PdfBrdf(mat, normal, viewDir, lightDir);
                    float lightPdf = 1f / (4f * Mathf.PI);
                    float weight = PowerHeuristic(lightPdf, brdfPdf);
                    result += brdf * env * weight / Mathf.Max(lightPdf, 1e-3f);
                }
            }

            return result;
        }

        static Vector3 SampleBrdf(
            LilToonParameters mat,
            Vector3 normal,
            Vector3 viewDir,
            Random rng,
            out SpectralColor brdf,
            out float pdf)
        {
            float diffWeight = 1f - mat.metallic;
            float specWeight = mat.metallic;
            float clearWeight = mat.clearCoat;
            float totalWeight = diffWeight + specWeight + clearWeight;

            float r = (float)rng.NextDouble() * Mathf.Max(totalWeight, 1e-3f);
            Vector3 dir;
            if (r < diffWeight)
            {
                dir = SampleHemisphere(normal, rng);
            }
            else
            {
                dir = Vector3.Reflect(-viewDir, normal).normalized;
            }

            brdf = EvaluateBrdf(mat.color, mat, normal, dir, viewDir);

            float cos = Mathf.Max(0f, Vector3.Dot(dir, normal));
            float diffusePdf = diffWeight / Mathf.Max(totalWeight, 1e-3f) * cos / Mathf.PI;
            float specPdf = 0f;
            Vector3 refl = Vector3.Reflect(-viewDir, normal).normalized;
            if (Vector3.Dot(refl, dir) > 0.999f)
                specPdf = (specWeight + clearWeight) / Mathf.Max(totalWeight, 1e-3f);

            pdf = diffusePdf + specPdf;
            return dir;
        }

        static Vector3 SampleHemisphere(Vector3 normal, Random rng)
        {
            float u1 = (float)rng.NextDouble();
            float u2 = (float)rng.NextDouble();
            float r = Mathf.Sqrt(u1);
            float theta = 2f * Mathf.PI * u2;
            float x = r * Mathf.Cos(theta);
            float y = r * Mathf.Sin(theta);
            float z = Mathf.Sqrt(1f - u1);

            Vector3 tangent = Vector3.Normalize(Vector3.Cross(normal, Mathf.Abs(normal.x) > 0.1f ? Vector3.up : Vector3.right));
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            return (x * tangent + y * bitangent + z * normal).normalized;
        }

        static Vector3 SampleSphere(Random rng)
        {
            float u1 = (float)rng.NextDouble();
            float u2 = (float)rng.NextDouble();
            float z = 1f - 2f * u1;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            float phi = 2f * Mathf.PI * u2;
            return new Vector3(r * Mathf.Cos(phi), z, r * Mathf.Sin(phi));
        }

        static SpectralColor SampleEnvironment(Color[] env, int width, int height, Vector3 dir)
        {
            float u = 0.5f + Mathf.Atan2(dir.z, dir.x) / (2f * Mathf.PI);
            float v = 0.5f - Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) / Mathf.PI;
            return SpectralColor.FromPixelData(env, width, height, u, v);
        }

        static Color SampleColor(Color[] pixels, int width, int height, float u, float v)
        {
            if (pixels == null || pixels.Length == 0)
                return Color.white;
            u = Mathf.Repeat(u, 1f);
            v = Mathf.Repeat(v, 1f);
            float x = u * (width - 1);
            float y = v * (height - 1);
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float tx = x - x0;
            float ty = y - y0;
            Color c00 = pixels[y0 * width + x0];
            Color c10 = pixels[y0 * width + x1];
            Color c01 = pixels[y1 * width + x0];
            Color c11 = pixels[y1 * width + x1];
            Color c0 = Color.Lerp(c00, c10, tx);
            Color c1 = Color.Lerp(c01, c11, tx);
            return Color.Lerp(c0, c1, ty);
        }

        static float PdfBrdf(LilToonParameters mat, Vector3 normal, Vector3 viewDir, Vector3 lightDir)
        {
            float diffWeight = 1f - mat.metallic;
            float specWeight = mat.metallic;
            float clearWeight = mat.clearCoat;
            float total = diffWeight + specWeight + clearWeight;

            float diffusePdf = Mathf.Max(0f, Vector3.Dot(normal, lightDir)) / Mathf.PI * diffWeight / Mathf.Max(total, 1e-3f);
            float specPdf = 0f;
            Vector3 refl = Vector3.Reflect(-viewDir, normal).normalized;
            if (Vector3.Dot(refl, lightDir) > 0.999f)
            {
                specPdf = (specWeight + clearWeight) / Mathf.Max(total, 1e-3f);
            }
            return diffusePdf + specPdf;
        }

        static float PowerHeuristic(float a, float b)
        {
            float a2 = a * a;
            float b2 = b * b;
            return a2 / (a2 + b2 + 1e-7f);
        }

        static SpectralColor EvaluateBrdf(SpectralColor albedo, LilToonParameters mat, Vector3 normal, Vector3 lightDir, Vector3 viewDir)
        {
            float metallic = mat.metallic;
            float roughness = mat.roughness;
            float clearCoat = mat.clearCoat;
            float clearCoatRoughness = mat.clearCoatRoughness;
            float sheen = mat.sheen;

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

            SpectralColor F0 = SpectralColor.Lerp(SpectralColor.FromRGB(new Color(0.04f, 0.04f, 0.04f)), albedo, metallic);
            SpectralColor F = F0 + (SpectralColor.White - F0) * Mathf.Pow(1f - vdoth, 5f);
            SpectralColor spec = F * (D * G / (4f * ndotv * ndotl + 1e-5f));

            // Diffuse term with optional sheen
            SpectralColor diffuse = (albedo / Mathf.PI) * (1f - metallic);
            if (sheen > 0f)
            {
                SpectralColor sheenColor = albedo * sheen * Mathf.Pow(1f - vdoth, 5f) * (1f - metallic);
                diffuse += sheenColor;
            }

            // Clear coat specular using a simple GGX lobe
            SpectralColor clear = SpectralColor.Black;
            if (clearCoat > 0f)
            {
                float ccRough = Mathf.Max(0.001f, clearCoatRoughness * clearCoatRoughness);
                float ccA = ccRough * ccRough;
                float ccDenom = ndoth * ndoth * (ccA - 1f) + 1f;
                float Dc = ccA / (Mathf.PI * ccDenom * ccDenom + 1e-7f);
                float kc = (ccRough + 1f); kc = (kc * kc) / 8f;
                float Gvc = ndotv / (ndotv * (1f - kc) + kc);
                float Glc = ndotl / (ndotl * (1f - kc) + kc);
                float Gc = Gvc * Glc;
                float Fc = 0.04f + 0.96f * Mathf.Pow(1f - vdoth, 5f);
                clear = SpectralColor.White * (Fc * (Dc * Gc / (4f * ndotv * ndotl + 1e-5f))) * clearCoat;
            }

            return (diffuse + spec + clear) * ndotl;
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

