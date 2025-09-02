using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System.IO;
using System.Runtime.InteropServices;

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

        [Range(1, 4096)]
        public int width = 2048;

        [Range(1, 4096)]
        public int height = 2048;

        [Range(1, 1024)]
        public int samplesPerPixel = 256;

        public int areaLightSamples = 4;

        [Range(1, 128)]
        public int maxDepth = 32;

        public int russianRouletteDepth = 3;
        public string environmentPath;


        public ComputeShader rayTracingShader;

        RenderTexture _output;
        Texture2D _cpuTexture;
        SpectralColor[] _accumulation;
        int _frameCount;
        List<BvhBuilder.BvhNode> _nodes;
        List<BvhBuilder.Triangle> _triangles;
        List<LilToonParameters> _materials;
        List<LightCollector.LightData> _lights;
        Texture2D _environment;
        Color[] _environmentPixels;
        int _envWidth;
        int _envHeight;

        Color[] _colorBuffer;
        readonly ThreadLocal<System.Random> _rng = new ThreadLocal<System.Random>(() => new System.Random(Guid.NewGuid().GetHashCode()));

        ComputeBuffer _triangleBuffer;
        ComputeBuffer _materialBuffer;
        ComputeBuffer _nodeBuffer;
        int _kernel;

        [StructLayout(LayoutKind.Sequential)]
        struct GpuTriangle
        {
            public Vector3 v0; public float pad0;
            public Vector3 v1; public float pad1;
            public Vector3 v2; public float pad2;
            public Vector3 n0; public float pad3;
            public Vector3 n1; public float pad4;
            public Vector3 n2; public float pad5;
            public int materialIndex; public Vector3 pad6;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct GpuMaterial
        {
            public Vector3 color; public float pad;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct GpuNode
        {
            public Vector3 min; public float pad0;
            public Vector3 max; public float pad1;
            public int left; public int right; public int start; public int count;
            public int next; public Vector3 pad2;
        }

        void OnEnable()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (sceneRoot == null && targetCamera != null)
                sceneRoot = targetCamera.gameObject;

            BuildScene();
            LoadEnvironment();
            InitTexture();
            if (rayTracingShader != null)
                _kernel = rayTracingShader.FindKernel("CSMain");
        }

        void OnDisable()
        {
            if (_output != null)
            {
                _output.Release();
                _output = null;
            }
            if (_cpuTexture != null)
            {
                DestroyImmediate(_cpuTexture);
                _cpuTexture = null;
            }
            if (_environment != null)
            {
                DestroyImmediate(_environment);
                _environment = null;
            }
            if (_triangleBuffer != null)
            {
                _triangleBuffer.Release();
                _triangleBuffer = null;
            }
            if (_materialBuffer != null)
            {
                _materialBuffer.Release();
                _materialBuffer = null;
            }
            if (_nodeBuffer != null)
            {
                _nodeBuffer.Release();
                _nodeBuffer = null;
            }
            _environmentPixels = null;
            _nodes = null;
            _triangles = null;
            _materials = null;
            _lights = null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                BuildScene();
                LoadEnvironment();
            }
        }
#endif

        void InitTexture()
        {
            if (_output == null || _output.width != width || _output.height != height)
            {
                if (_output != null)
                    _output.Release();
                _output = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat)
                {
                    enableRandomWrite = true,
                    wrapMode = TextureWrapMode.Clamp
                };
                _output.Create();

                _cpuTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp
                };

                _accumulation = new SpectralColor[width * height];
                _frameCount = 0;
            }
        }

        void BuildScene()
        {
            var meshes = GeometryCollector.Collect(sceneRoot);
            _nodes = BvhBuilder.Build(meshes, out _triangles, out _materials);
            _lights = LightCollector.Collect(sceneRoot);
            UpdateComputeBuffers();
        }

        void LoadEnvironment()
        {
            if (string.IsNullOrEmpty(environmentPath))
                return;
            try
            {
                byte[] data = File.ReadAllBytes(environmentPath);
                _environment = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _environment.wrapMode = TextureWrapMode.Clamp;
                _environment.LoadImage(data);
                _environmentPixels = _environment.GetPixels();
                _envWidth = _environment.width;
                _envHeight = _environment.height;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load environment: {e.Message}");
                _environment = null;
                _environmentPixels = null;
            }
        }

        void Update()
        {
            width = Mathf.Clamp(width, 1, 4096);
            height = Mathf.Clamp(height, 1, 4096);
            samplesPerPixel = Mathf.Clamp(samplesPerPixel, 1, 1024);
            maxDepth = Mathf.Clamp(maxDepth, 1, 128);

            if (HierarchyChanged(sceneRoot != null ? sceneRoot.transform : null))
            {
                BuildScene();
            }
            InitTexture();
            Render();
        }

        bool HierarchyChanged(Transform root)
        {
            if (root == null) return false;
            bool changed = root.hasChanged;
            root.hasChanged = false;
            for (int i = 0; i < root.childCount; ++i)
                changed |= HierarchyChanged(root.GetChild(i));
            return changed;
        }

        void UpdateComputeBuffers()
        {
            if (rayTracingShader == null)
                return;

            if (_triangleBuffer != null)
            {
                _triangleBuffer.Release();
                _triangleBuffer = null;
            }
            if (_materialBuffer != null)
            {
                _materialBuffer.Release();
                _materialBuffer = null;
            }
            if (_nodeBuffer != null)
            {
                _nodeBuffer.Release();
                _nodeBuffer = null;
            }

            if (_triangles != null && _triangles.Count > 0)
            {
                var gpuTris = new GpuTriangle[_triangles.Count];
                for (int i = 0; i < _triangles.Count; i++)
                {
                    var t = _triangles[i];
                    gpuTris[i] = new GpuTriangle
                    {
                        v0 = t.v0,
                        v1 = t.v1,
                        v2 = t.v2,
                        n0 = t.n0,
                        n1 = t.n1,
                        n2 = t.n2,
                        materialIndex = t.materialIndex
                    };
                }
                _triangleBuffer = new ComputeBuffer(gpuTris.Length, Marshal.SizeOf(typeof(GpuTriangle)));
                _triangleBuffer.SetData(gpuTris);
            }

            if (_materials != null && _materials.Count > 0)
            {
                var gpuMats = new GpuMaterial[_materials.Count];
                for (int i = 0; i < _materials.Count; i++)
                {
                    Color rgb = _materials[i].color.ToRGB();
                    gpuMats[i] = new GpuMaterial { color = new Vector3(rgb.r, rgb.g, rgb.b) };
                }
                _materialBuffer = new ComputeBuffer(gpuMats.Length, Marshal.SizeOf(typeof(GpuMaterial)));
                _materialBuffer.SetData(gpuMats);
            }

            if (_nodes != null && _nodes.Count > 0)
            {
                var gpuNodes = new GpuNode[_nodes.Count];
                for (int i = 0; i < _nodes.Count; i++)
                {
                    var n = _nodes[i];
                    gpuNodes[i] = new GpuNode
                    {
                        min = n.bounds.min,
                        max = n.bounds.max,
                        left = n.left,
                        right = n.right,
                        start = n.start,
                        count = n.count,
                        next = n.next
                    };
                }
                _nodeBuffer = new ComputeBuffer(gpuNodes.Length, Marshal.SizeOf(typeof(GpuNode)));
                _nodeBuffer.SetData(gpuNodes);
            }

            if (rayTracingShader != null)
            {
                if (_triangleBuffer != null)
                    rayTracingShader.SetBuffer(_kernel, "_Triangles", _triangleBuffer);
                if (_materialBuffer != null)
                    rayTracingShader.SetBuffer(_kernel, "_Materials", _materialBuffer);
                if (_nodeBuffer != null)
                    rayTracingShader.SetBuffer(_kernel, "_Nodes", _nodeBuffer);
            }
        }

        static Color ToneMap(Color c)
        {
            c.r = c.r / (c.r + 1f);
            c.g = c.g / (c.g + 1f);
            c.b = c.b / (c.b + 1f);
            return c;
        }

        void Render()
        {
            if (targetCamera == null || _nodes == null)
                return;

            if (rayTracingShader != null)
            {
                rayTracingShader.SetInt("_NumTriangles", _triangles != null ? _triangles.Count : 0);
                rayTracingShader.SetVector("_CameraPos", targetCamera.transform.position);
                rayTracingShader.SetVector("_CameraForward", targetCamera.transform.forward);
                rayTracingShader.SetVector("_CameraRight", targetCamera.transform.right);
                rayTracingShader.SetVector("_CameraUp", targetCamera.transform.up);
                rayTracingShader.SetFloat("_TanFov", Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f));
                rayTracingShader.SetFloat("_Aspect", (float)width / height);
                rayTracingShader.SetTexture(_kernel, "Result", _output);
                int tx = Mathf.CeilToInt(width / 8f);
                int ty = Mathf.CeilToInt(height / 8f);
                rayTracingShader.Dispatch(_kernel, tx, ty, 1);
                Shader.SetGlobalTexture("_lilSoftwareRayTex", _output);
                return;
            }

            if (_accumulation == null || _accumulation.Length != width * height)
            {
                _accumulation = new SpectralColor[width * height];
                _frameCount = 0;
            }

            if (_colorBuffer == null || _colorBuffer.Length != width * height)
                _colorBuffer = new Color[width * height];

            var camParams = new RayGenerator.CameraParams
            {
                position = targetCamera.transform.position,
                forward = targetCamera.transform.forward,
                right = targetCamera.transform.right,
                up = targetCamera.transform.up,
                tanFov = Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f),
                aspect = (float)width / height
            };

            var colors = _colorBuffer;
            int frameIndex = _frameCount + 1;
            Parallel.For(0, height, y =>
            {
                var rng = _rng.Value;
                for (int x = 0; x < width; ++x)
                {
                    SpectralColor col = SpectralColor.Black;
                    for (int s = 0; s < samplesPerPixel; ++s)
                    {
                        var offset = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                        Ray ray = RayGenerator.Generate(camParams, x, y, width, height, offset);
                        col += Shading.Shade(ray, _nodes, _triangles, _materials, _lights, _environmentPixels, _envWidth, _envHeight, areaLightSamples, maxDepth, russianRouletteDepth, rng);
                    }
                    col /= samplesPerPixel;
                    int idx = y * width + x;
                    _accumulation[idx] += col;
                    colors[idx] = ToneMap((_accumulation[idx] / frameIndex).ToRGB());
                }
            });
            _frameCount = frameIndex;
            _cpuTexture.SetPixels(colors);
            _cpuTexture.Apply();
            Shader.SetGlobalTexture("_lilSoftwareRayTex", _cpuTexture);
        }
    }
}

