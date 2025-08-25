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
        /// <param name="x">Pixel x coordinate.</param>
        /// <param name="y">Pixel y coordinate.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        public static Ray Generate(Camera camera, int x, int y, int width, int height)
        {
            float u = (x + 0.5f) / width;
            float v = (y + 0.5f) / height;
            return camera.ViewportPointToRay(new Vector3(u, v, 0f));
        }
    }
}
