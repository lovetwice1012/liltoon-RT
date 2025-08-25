using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Generates camera rays for software ray tracing.
    /// </summary>
    public static class RayGenerator
    {
        /// <summary>
        /// Cached parameters describing a camera. These values can be
        /// accessed safely from worker threads without touching the
        /// Unity API.
        /// </summary>
        public struct CameraParams
        {
            public Vector3 position;
            public Vector3 forward;
            public Vector3 right;
            public Vector3 up;
            public float tanFov;
            public float aspect;
        }

        /// <summary>
        /// Create a ray going through a pixel on the screen using
        /// precomputed <see cref="CameraParams"/> instead of accessing
        /// Unity's <see cref="Camera"/> API from worker threads.
        /// </summary>
        /// <param name="cam">Camera parameters captured on the main thread.</param>
        /// <param name="x">Pixel x coordinate with subpixel offset.</param>
        /// <param name="y">Pixel y coordinate with subpixel offset.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="pixelOffset">Random sub-pixel offset.</param>
        public static Ray Generate(CameraParams cam, int x, int y, int width, int height, Vector2 pixelOffset)
        {
            float u = ((x + pixelOffset.x) / width - 0.5f) * 2f;
            float v = ((y + pixelOffset.y) / height - 0.5f) * 2f;
            Vector3 dir = cam.forward + cam.right * (u * cam.tanFov * cam.aspect) + cam.up * (v * cam.tanFov);
            dir.Normalize();
            return new Ray(cam.position, dir);
        }
    }
}
