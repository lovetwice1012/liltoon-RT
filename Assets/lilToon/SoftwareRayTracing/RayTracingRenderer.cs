using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.IO;

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
        public int samplesPerPixel = 1;
        public int areaLightSamples = 4;
        public int maxDepth = 8;
        public int russianRouletteDepth = 3;
        public string environmentPath;


        Texture2D _output;
        SpectralColor[] _accumulation;
        int _frameCount;
        List<BvhBuilder.BvhNode> _nodes;
        List<BvhBuilder.Triangle> _triangles;
        List<LightCollector.LightData> _lights;
        Texture2D _environment;

        void OnEnable()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (sceneRoot == null && targetCamera != null)
                sceneRoot = targetCamera.gameObject;

            BuildScene();
            LoadEnvironment();
            InitTexture();
        }

        void InitTexture()
        {
            if (_output == null || _output.width != width || _output.height != height)
            {
                _output = new Texture2D(width, height, TextureFormat.RGBA32, false);
                _output.wrapMode = TextureWrapMode.Clamp;
                Shader.SetGlobalTexture("_lilSoftwareRayTex", _output);

                _accumulation = new SpectralColor[width * height];
                _frameCount = 0;
            }
        }

        void BuildScene()
        {
            var meshes = GeometryCollector.Collect(sceneRoot);
            _nodes = BvhBuilder.Build(meshes, out _triangles);
            _lights = LightCollector.Collect(sceneRoot);
        }

        void LoadEnvironment()
        {
            if (string.IsNullOrEmpty(environmentPath))
                return;
            try
            {
                byte[] data = System.IO.File.ReadAllBytes(environmentPath);
                _environment = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _environment.wrapMode = TextureWrapMode.Clamp;
                _environment.LoadImage(data);
            }
            catch (Exception)
            {
                _environment = null;
            }
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

            if (_accumulation == null || _accumulation.Length != width * height)
            {
                _accumulation = new SpectralColor[width * height];
                _frameCount = 0;
            }

            var colors = new Color[width * height];
            int frameIndex = _frameCount + 1;
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; ++x)
                {
                    var rng = new Random(x * 73856093 ^ y * 19349663 ^ frameIndex);
                    SpectralColor col = SpectralColor.Black;
                    for (int s = 0; s < samplesPerPixel; ++s)
                    {
                        var offset = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                        Ray ray = RayGenerator.Generate(targetCamera, x, y, width, height, offset);
                        col += Shading.Shade(ray, _nodes, _triangles, _lights, _environment, areaLightSamples, maxDepth, russianRouletteDepth, rng);
                    }
                    col /= samplesPerPixel;
                    int idx = y * width + x;
                    _accumulation[idx] += col;
                    colors[idx] = (_accumulation[idx] / frameIndex).ToRGB();
                }
            });
            _frameCount = frameIndex;
            _output.SetPixels(colors);
            _output.Apply();
            Shader.SetGlobalTexture("_lilSoftwareRayTex", _output);
        }
    }
}

