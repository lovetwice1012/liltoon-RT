using System.Collections.Generic;
using UnityEngine;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Integrates the software ray tracer output into Unity by rendering
    /// the scene to a texture and exposing it as a global shader property.
    /// </summary>
    [ExecuteAlways]
    public class RayTracingRenderer : MonoBehaviour
    {
        public Camera targetCamera;
        public GameObject sceneRoot;
        public int width = 256;
        public int height = 256;
        public int samples = 16;

        Texture2D _output;
        List<BvhBuilder.BvhNode> _nodes;
        List<BvhBuilder.Triangle> _triangles;
        List<LightCollector.LightData> _lights;

        void OnEnable()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (sceneRoot == null && targetCamera != null)
                sceneRoot = targetCamera.gameObject;

            BuildScene();
            InitTexture();
        }

        void InitTexture()
        {
            if (_output == null || _output.width != width || _output.height != height)
            {
                _output = new Texture2D(width, height, TextureFormat.RGBA32, false);
                _output.wrapMode = TextureWrapMode.Clamp;
                Shader.SetGlobalTexture("_lilSoftwareRayTex", _output);
            }
        }

        void BuildScene()
        {
            var meshes = GeometryCollector.Collect(sceneRoot);
            _nodes = BvhBuilder.Build(meshes, out _triangles);
            _lights = LightCollector.Collect(sceneRoot);
        }

        void Update()
        {
            InitTexture();
            Render();
        }

        void Render()
        {
            if (targetCamera == null || _nodes == null)
                return;

            var colors = new Color[width * height];
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    Color col = Color.black;
                    for (int s = 0; s < samples; ++s)
                    {
                        float u = x + Random.value;
                        float v = y + Random.value;
                        Ray ray = RayGenerator.Generate(targetCamera, u, v, width, height);
                        col += Shading.Shade(ray, _nodes, _triangles, _lights);
                    }
                    colors[y * width + x] = col / samples;
                }
            }
            _output.SetPixels(colors);
            _output.Apply();
            Shader.SetGlobalTexture("_lilSoftwareRayTex", _output);
        }
    }
}

