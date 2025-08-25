using System.Collections.Generic;
using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Collects light data from Unity Light components.
    /// </summary>
    public static class LightCollector
    {
        public struct LightData
        {
            public Vector3 position;
            public Color color;
            public float intensity;
            public LightType type;
        }

        /// <summary>
        /// Extracts light data under the specified root object.
        /// </summary>
        public static List<LightData> Collect(GameObject root)
        {
            var result = new List<LightData>();
            if (root == null) return result;

            foreach (var light in root.GetComponentsInChildren<Light>())
            {
                result.Add(new LightData
                {
                    position = light.transform.position,
                    color = light.color,
                    intensity = light.intensity,
                    type = light.type
                });
            }

            return result;
        }
    }
}

