using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Represents a spectral power distribution sampled at three wavelengths.
    /// For simplicity this implementation stores CIE XYZ tristimulus values
    /// and provides conversion utilities to and from linear sRGB.
    /// </summary>
    public struct SpectralColor
    {
        public Vector3 xyz;

        public SpectralColor(Vector3 xyz)
        {
            this.xyz = xyz;
        }

        public static SpectralColor FromRGB(Color rgb)
        {
            // Assume the input is in linear sRGB space. Convert to CIE XYZ.
            float X = 0.4124f * rgb.r + 0.3576f * rgb.g + 0.1805f * rgb.b;
            float Y = 0.2126f * rgb.r + 0.7152f * rgb.g + 0.0722f * rgb.b;
            float Z = 0.0193f * rgb.r + 0.1192f * rgb.g + 0.9505f * rgb.b;
            return new SpectralColor(new Vector3(X, Y, Z));
        }

        public static SpectralColor FromTexture(Texture2D tex, float u, float v)
        {
            return FromRGB(tex.GetPixelBilinear(u, v));
        }

        public Color ToRGB()
        {
            float r =  3.2406f * xyz.x - 1.5372f * xyz.y - 0.4986f * xyz.z;
            float g = -0.9689f * xyz.x + 1.8758f * xyz.y + 0.0415f * xyz.z;
            float b =  0.0557f * xyz.x - 0.2040f * xyz.y + 1.0570f * xyz.z;
            return new Color(r, g, b, 1f);
        }

        public float MaxComponent => Mathf.Max(xyz.x, Mathf.Max(xyz.y, xyz.z));

        public static SpectralColor operator +(SpectralColor a, SpectralColor b)
            => new SpectralColor(a.xyz + b.xyz);

        public static SpectralColor operator -(SpectralColor a, SpectralColor b)
            => new SpectralColor(a.xyz - b.xyz);

        public static SpectralColor operator *(SpectralColor a, SpectralColor b)
            => new SpectralColor(Vector3.Scale(a.xyz, b.xyz));

        public static SpectralColor operator *(SpectralColor a, float b)
            => new SpectralColor(a.xyz * b);

        public static SpectralColor operator *(float b, SpectralColor a)
            => new SpectralColor(a.xyz * b);

        public static SpectralColor operator /(SpectralColor a, float b)
            => new SpectralColor(a.xyz / b);

        public static SpectralColor Lerp(SpectralColor a, SpectralColor b, float t)
            => new SpectralColor(Vector3.Lerp(a.xyz, b.xyz, t));

        public static SpectralColor Black => new SpectralColor(Vector3.zero);
        public static SpectralColor White => FromRGB(Color.white);
    }
}
