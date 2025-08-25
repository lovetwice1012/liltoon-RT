using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Extracts lilToon material parameters in a form suitable for the software ray tracer.
    /// </summary>
    [System.Serializable]
    public struct LilToonParameters
    {
        public SpectralColor color;
        public float metallic;
        public float roughness;
        public float clearCoat;
        public float clearCoatRoughness;
        public float sheen;

        // Pre-baked texture data for thread-safe sampling
        public Color[] albedoPixels;
        public int albedoWidth;
        public int albedoHeight;

        public Color[] normalPixels;
        public int normalWidth;
        public int normalHeight;
    }

    public static class ParameterExtractor
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        static readonly int ClearCoatId = Shader.PropertyToID("_ClearCoat");
        static readonly int ClearCoatRoughnessId = Shader.PropertyToID("_ClearCoatRoughness");
        static readonly int SheenId = Shader.PropertyToID("_Sheen");
        /// <summary>
        /// Creates a <see cref="LilToonParameters"/> snapshot from a material while
        /// preserving lilToon parameter compatibility.
        /// </summary>
        public static LilToonParameters FromMaterial(Material material)
        {
            LilToonParameters param = new LilToonParameters();
            if(material == null) return param;

            param.color = SpectralColor.FromRGB(material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white);
            param.metallic = material.HasProperty(MetallicId) ? material.GetFloat(MetallicId) : 0f;
            // lilToon uses smoothness; convert to roughness for ray tracing.
            param.roughness = material.HasProperty(SmoothnessId) ? 1f - material.GetFloat(SmoothnessId) : 1f;
            param.clearCoat = material.HasProperty(ClearCoatId) ? material.GetFloat(ClearCoatId) : 0f;
            param.clearCoatRoughness = material.HasProperty(ClearCoatRoughnessId) ? material.GetFloat(ClearCoatRoughnessId) : 0f;
            param.sheen = material.HasProperty(SheenId) ? material.GetFloat(SheenId) : 0f;

            Texture2D albedoTex = material.HasProperty(MainTexId) ? material.GetTexture(MainTexId) as Texture2D : null;
            if (albedoTex != null && albedoTex.isReadable)
            {
                param.albedoPixels = albedoTex.GetPixels();
                param.albedoWidth = albedoTex.width;
                param.albedoHeight = albedoTex.height;
            }

            Texture2D normalTex = material.HasProperty(BumpMapId) ? material.GetTexture(BumpMapId) as Texture2D : null;
            if (normalTex != null && normalTex.isReadable)
            {
                param.normalPixels = normalTex.GetPixels();
                param.normalWidth = normalTex.width;
                param.normalHeight = normalTex.height;
            }
            return param;
        }
    }
}
