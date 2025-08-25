using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Extracts lilToon material parameters in a form suitable for the software ray tracer.
    /// </summary>
    [System.Serializable]
    public struct LilToonParameters
    {
        public Color color;
        public float metallic;
        public float roughness;
        public Texture2D albedoMap;
        public Texture2D normalMap;
    }

    public static class ParameterExtractor
    {
        /// <summary>
        /// Creates a <see cref="LilToonParameters"/> snapshot from a material while
        /// preserving lilToon parameter compatibility.
        /// </summary>
        public static LilToonParameters FromMaterial(Material material)
        {
            LilToonParameters param = new LilToonParameters();
            if(material == null) return param;

            param.color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            param.metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            // lilToon uses smoothness; convert to roughness for ray tracing.
            param.roughness = material.HasProperty("_Smoothness") ? 1f - material.GetFloat("_Smoothness") : 1f;
            param.albedoMap = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") as Texture2D : null;
            param.normalMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") as Texture2D : null;
            return param;
        }
    }
}
