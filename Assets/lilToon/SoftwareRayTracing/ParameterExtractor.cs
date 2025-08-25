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
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        /// <summary>
        /// Creates a <see cref="LilToonParameters"/> snapshot from a material while
        /// preserving lilToon parameter compatibility.
        /// </summary>
        public static LilToonParameters FromMaterial(Material material)
        {
            LilToonParameters param = new LilToonParameters();
            if(material == null) return param;

            param.color = material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
            param.metallic = material.HasProperty(MetallicId) ? material.GetFloat(MetallicId) : 0f;
            // lilToon uses smoothness; convert to roughness for ray tracing.
            param.roughness = material.HasProperty(SmoothnessId) ? 1f - material.GetFloat(SmoothnessId) : 1f;
            param.albedoMap = material.HasProperty(MainTexId) ? material.GetTexture(MainTexId) as Texture2D : null;
            param.normalMap = material.HasProperty(BumpMapId) ? material.GetTexture(BumpMapId) as Texture2D : null;
            return param;
        }
    }
}
