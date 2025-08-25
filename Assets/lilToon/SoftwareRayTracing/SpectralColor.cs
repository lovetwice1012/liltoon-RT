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

        public static SpectralColor FromPixelData(Color[] pixels, int width, int height, float u, float v)
        {
            if (pixels == null || pixels.Length == 0)
                return Black;
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
            Color c = Color.Lerp(c0, c1, ty);
            return FromRGB(c);
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
