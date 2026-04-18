using RRX.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RRX.Editor
{
    /// <summary>
    /// Bounded MR play space: passthrough/open center; virtual walls at the perimeter (square from origin,
    /// half-size <see cref="RRXPlayArea.RadiusMeters"/>). Interior partitions carve walkable lanes.
    /// </summary>
    static class RRXCubeBlockoutMenu
    {
        const string MatFolder = "Assets/RRX/Materials";

        const float WallHeight = 2.85f;
        const float WallThickness = 0.2f;
        const float PartitionThickness = 0.12f;

        [MenuItem("RRX/Generate MR Cube Blockout", false, 40)]
        [MenuItem("Window/RRX/Generate MR Cube Blockout", false, 40)]
        static void GenerateBlockout()
        {
            RunBlockoutGeneration();
        }

        /// <summary>Called by menu and <see cref="RRXDemoSceneWizard"/> full auto-build.</summary>
        public static void RunBlockoutGeneration()
        {
            EnsureMaterialFolder();
            float R = RRXPlayArea.RadiusMeters;
            float t = WallThickness;
            float H = WallHeight;

            var floorMat = GetOrCreateMat("RRX_Mat_Floor", new Color(0.42f, 0.42f, 0.44f));
            var boundaryMat = GetOrCreateMat("RRX_Mat_Boundary", new Color(0.22f, 0.24f, 0.28f));
            var interiorWallMat = GetOrCreateMat("RRX_Mat_Wall", new Color(0.55f, 0.58f, 0.62f));
            var accentMat = GetOrCreateMat("RRX_Mat_Accent", new Color(0.85f, 0.88f, 0.9f));
            var propMat = GetOrCreateMat("RRX_Mat_Prop", new Color(0.35f, 0.55f, 0.58f));

            var root = GameObject.Find("RRX_Environment_Root");
            if (root == null)
            {
                root = new GameObject("RRX_Environment_Root");
                Undo.RegisterCreatedObjectUndo(root, "RRX Environment Root");
            }

            ClearEnvironmentChildren(root.transform);

            // Floor: square play surface centered at origin (walkable domain).
            BuildCube(root.transform, "Floor_Slab", new Vector3(0f, -0.025f, 0f),
                new Vector3(2f * R, 0.05f, 2f * R), floorMat);

            // Outer boundary — virtual “walls around your domain” (MR: real world inside, cubes at the edge).
            var span = 2f * R + 2f * t;
            BuildCube(root.transform, "Boundary_North", new Vector3(0f, H * 0.5f, R + t * 0.5f),
                new Vector3(span, H, t), boundaryMat);
            BuildCube(root.transform, "Boundary_South", new Vector3(0f, H * 0.5f, -(R + t * 0.5f)),
                new Vector3(span, H, t), boundaryMat);
            BuildCube(root.transform, "Boundary_East", new Vector3(R + t * 0.5f, H * 0.5f, 0f),
                new Vector3(t, H, span), boundaryMat);
            BuildCube(root.transform, "Boundary_West", new Vector3(-(R + t * 0.5f), H * 0.5f, 0f),
                new Vector3(t, H, span), boundaryMat);

            // Interior carve-up (lighter partitions — alleys / stall feel).
            BuildCube(root.transform, "Interior_Lane_A", new Vector3(-R * 0.38f, H * 0.45f, 0f),
                new Vector3(PartitionThickness, H * 0.9f, R * 1.05f), interiorWallMat);
            BuildCube(root.transform, "Interior_Lane_B", new Vector3(R * 0.28f, H * 0.42f, R * 0.35f),
                new Vector3(R * 0.5f, H * 0.78f, PartitionThickness), interiorWallMat);
            BuildCube(root.transform, "Stall_Partition", new Vector3(-R * 0.15f, H * 0.44f, -R * 0.35f),
                new Vector3(R * 0.35f, H * 0.82f, PartitionThickness), accentMat);

            // Props — scaled to stay inside the play radius.
            BuildCube(root.transform, "Prop_SinkCounter", new Vector3(R * 0.62f, 1.05f, R * 0.15f),
                new Vector3(R * 0.38f, 0.35f, R * 0.22f), propMat);
            BuildCube(root.transform, "Prop_SinkSplash", new Vector3(R * 0.62f, 1.55f, R * 0.28f),
                new Vector3(R * 0.36f, 0.8f, 0.08f), accentMat);
            BuildCube(root.transform, "Prop_TrashBin", new Vector3(-R * 0.55f, 0.35f, R * 0.55f),
                new Vector3(0.55f, 0.7f, 0.55f), interiorWallMat);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log(
                $"[RRX] Cube blockout: play area ±{R} m on X/Z (edit RRXPlayArea.RadiusMeters for 3 m or 5 m). Save the scene.");
        }

        static void ClearEnvironmentChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                Undo.DestroyObjectImmediate(child);
            }
        }

        static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/RRX"))
                AssetDatabase.CreateFolder("Assets", "RRX");
            if (!AssetDatabase.IsValidFolder(MatFolder))
                AssetDatabase.CreateFolder("Assets/RRX", "Materials");
        }

        static Material GetOrCreateMat(string assetName, Color color)
        {
            var path = $"{MatFolder}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            if (mat.HasProperty("_Color"))
                mat.color = color;
            else if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void BuildCube(Transform parent, string name, Vector3 localPos, Vector3 localScale,
            Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);

            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null && mat != null)
            {
                Undo.RecordObject(r, "RRX Blockout mat");
                r.sharedMaterial = mat;
            }
        }
    }
}
