using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Generates camera rays for software ray tracing.
    /// </summary>
    public static class RayGenerator
    {
        /// <summary>
        /// Create a ray going through a pixel on the screen.
        /// </summary>
        /// <param name="camera">Camera used for generating the ray.</param>
        /// <param name="x">Pixel x coordinate with subpixel offset.</param>
        /// <param name="y">Pixel y coordinate with subpixel offset.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        public static Ray Generate(Camera camera, float x, float y, int width, int height)
        {
            float u = x / width;
            float v = y / height;
            return camera.ViewportPointToRay(new Vector3(u, v, 0f));
        }
    }
}
