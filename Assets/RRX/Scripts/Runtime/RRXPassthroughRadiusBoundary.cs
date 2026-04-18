using System.Collections.Generic;
using RRX.Core;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;

namespace RRX.Runtime
{
    /// <summary>
    /// Horizontal passthrough cutoff: an uncapped vertical tube (open top and bottom) follows the XR camera.
    /// Side walls sit at <see cref="_radiusMeters"/> horizontally; rays straight up/down miss the mesh so
    /// passthrough still shows zenith/nadir. (A full sphere centered on the eyes blocks the entire view —
    /// every ray hits the inner surface — so it is not used.)
    /// </summary>
    public sealed class RRXPassthroughRadiusBoundary : MonoBehaviour
    {
        const string OccluderShaderName = "RRX/PassthroughOccluder";
        const string TubeChildName = "RRX_PassthroughOccluderTube";
        const string LegacySphereChildName = "RRX_PassthroughOccluderSphere";

        [SerializeField] bool _syncRadiusFromPlayArea = true;

        [SerializeField] float _radiusMeters = 5f;
        [Tooltip("Tube extends ±this many meters along world Y from the occluder root (should cover view frustum).")]
        [SerializeField] float _tubeHalfHeightMeters = 40f;
        [SerializeField] [Range(8, 96)] int _segments = 48;

        [SerializeField] Camera _camera;
        [Tooltip("If true, the tube does not roll/pitch with the headset — only position follows the camera.")]
        [SerializeField] bool _worldAlignedRotation = true;

        MeshFilter _meshFilter;
        Mesh _tubeMesh;
        Material _occluderMaterial;

        void Awake()
        {
            ResolveCamera();
            ApplyPlayAreaRadiusIfSynced();
            EnsureOccluderTube();
        }

        void ApplyPlayAreaRadiusIfSynced()
        {
            if (_syncRadiusFromPlayArea)
                _radiusMeters = RRXPlayArea.VirtualFloorHoleRadiusMeters;
        }

        void OnDestroy()
        {
            DestroyTubeMeshAsset();
            if (_occluderMaterial != null)
                Destroy(_occluderMaterial);
        }

        void DestroyTubeMeshAsset()
        {
            if (_tubeMesh == null)
                return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(_tubeMesh);
            else
#endif
                Destroy(_tubeMesh);
            _tubeMesh = null;
        }

        void LateUpdate()
        {
            if (!isActiveAndEnabled || _camera == null)
                return;

            transform.position = _camera.transform.position;
            if (_worldAlignedRotation)
                transform.rotation = Quaternion.identity;
            else
                transform.rotation = _camera.transform.rotation;
        }

        void ResolveCamera()
        {
            if (_camera != null)
                return;

            var origin = FindObjectOfType<XROrigin>();
            if (origin != null && origin.Camera != null)
            {
                _camera = origin.Camera;
                return;
            }

            _camera = Camera.main;
        }

        void EnsureOccluderTube()
        {
            RemoveLegacySphereIfPresent();

            var existing = transform.Find(TubeChildName);
            if (existing != null)
            {
                _meshFilter = existing.GetComponent<MeshFilter>();
                var renderer = existing.GetComponent<MeshRenderer>();
                ApplyMaterialIfNeeded(renderer);
                RebuildTubeMesh();
                return;
            }

            var tube = new GameObject(TubeChildName);
            tube.transform.SetParent(transform, false);
            tube.transform.localPosition = Vector3.zero;
            tube.transform.localRotation = Quaternion.identity;
            tube.transform.localScale = Vector3.one;

            _meshFilter = tube.AddComponent<MeshFilter>();
            var meshRenderer = tube.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            ApplyMaterialIfNeeded(meshRenderer);
            RebuildTubeMesh();
        }

        void RemoveLegacySphereIfPresent()
        {
            var legacy = transform.Find(LegacySphereChildName);
            if (legacy == null)
                return;

            var go = legacy.gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(go);
            else
#endif
                Destroy(go);
        }

        void ApplyMaterialIfNeeded(MeshRenderer renderer)
        {
            if (renderer == null)
                return;

            var shader = Shader.Find(OccluderShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[{nameof(RRXPassthroughRadiusBoundary)}] Shader '{OccluderShaderName}' not found. " +
                    "Import Assets/RRX/Shaders/RRXPassthroughOccluder.shader.");
                return;
            }

            if (_occluderMaterial == null)
                _occluderMaterial = new Material(shader);

            renderer.sharedMaterial = _occluderMaterial;
        }

        void RebuildTubeMesh()
        {
            if (_meshFilter == null)
                return;

            DestroyTubeMeshAsset();

            _tubeMesh = BuildOpenTubeMesh(_radiusMeters, _tubeHalfHeightMeters, _segments);
            _meshFilter.sharedMesh = _tubeMesh;
        }

        /// <summary>Open-ended tube: vertical strip only (no caps). Axis aligned with world Y.</summary>
        static Mesh BuildOpenTubeMesh(float innerRadius, float halfHeight, int segments)
        {
            var mesh = new Mesh { name = "RRX_OpenPassthroughTube" };

            var vertices = new List<Vector3>((segments + 1) * 2);
            var normals = new List<Vector3>((segments + 1) * 2);
            var triangles = new List<int>(segments * 6);

            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments * (Mathf.PI * 2f);
                var c = Mathf.Cos(t);
                var s = Mathf.Sin(t);
                var radial = new Vector3(c, 0f, s);

                vertices.Add(radial * innerRadius + new Vector3(0f, -halfHeight, 0f));
                vertices.Add(radial * innerRadius + new Vector3(0f, halfHeight, 0f));
                normals.Add(radial);
                normals.Add(radial);
            }

            for (var i = 0; i < segments; i++)
            {
                var b0 = i * 2;
                var t0 = i * 2 + 1;
                var b1 = (i + 1) * 2;
                var t1 = (i + 1) * 2 + 1;

                triangles.Add(b0);
                triangles.Add(t0);
                triangles.Add(b1);
                triangles.Add(b1);
                triangles.Add(t0);
                triangles.Add(t1);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnValidate()
        {
            ApplyPlayAreaRadiusIfSynced();
            _radiusMeters = Mathf.Max(0.1f, _radiusMeters);
            _tubeHalfHeightMeters = Mathf.Max(2f, _tubeHalfHeightMeters);
            _segments = Mathf.Clamp(_segments, 8, 96);
            if (_meshFilter != null)
                RebuildTubeMesh();
        }
    }
}
