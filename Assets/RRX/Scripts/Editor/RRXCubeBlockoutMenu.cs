using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RRX.Editor
{
    /// <summary>Prototype restroom / alley volume using only scaled cubes (hackathon scope).</summary>
    static class RRXCubeBlockoutMenu
    {
        const string MatFolder = "Assets/RRX/Materials";

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
            var floorMat = GetOrCreateMat("RRX_Mat_Floor", new Color(0.42f, 0.42f, 0.44f));
            var wallMat = GetOrCreateMat("RRX_Mat_Wall", new Color(0.55f, 0.58f, 0.62f));
            var accentMat = GetOrCreateMat("RRX_Mat_Accent", new Color(0.85f, 0.88f, 0.9f));
            var propMat = GetOrCreateMat("RRX_Mat_Prop", new Color(0.35f, 0.55f, 0.58f));

            var root = GameObject.Find("RRX_Environment_Root");
            if (root == null)
            {
                root = new GameObject("RRX_Environment_Root");
                Undo.RegisterCreatedObjectUndo(root, "RRX Environment Root");
            }

            BuildCube(root.transform, "Floor_Slab", new Vector3(0f, -0.025f, 2f),
                new Vector3(14f, 0.05f, 12f), floorMat);
            BuildCube(root.transform, "Wall_Back", new Vector3(0f, 1.5f, 8f),
                new Vector3(14f, 3f, 0.2f), wallMat);
            BuildCube(root.transform, "Wall_Left", new Vector3(-7f, 1.5f, 2f),
                new Vector3(0.2f, 3f, 12f), wallMat);
            BuildCube(root.transform, "Wall_Right", new Vector3(7f, 1.5f, 2f),
                new Vector3(0.2f, 3f, 12f), wallMat);
            BuildCube(root.transform, "Stall_Partition", new Vector3(-2f, 1.25f, 4.5f),
                new Vector3(0.15f, 2.5f, 4f), accentMat);
            BuildCube(root.transform, "Prop_SinkCounter", new Vector3(5.2f, 1.05f, 3f),
                new Vector3(2.2f, 0.35f, 1.1f), propMat);
            BuildCube(root.transform, "Prop_SinkSplash", new Vector3(5.2f, 1.55f, 3.25f),
                new Vector3(2f, 0.8f, 0.08f), accentMat);
            BuildCube(root.transform, "Prop_TrashBin", new Vector3(-5f, 0.35f, 5.5f),
                new Vector3(0.55f, 0.7f, 0.55f), wallMat);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[RRX] Cube blockout generated under RRX_Environment_Root. Save the scene.");
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
            var t = parent.Find(name);
            GameObject go;
            if (t != null)
            {
                go = t.gameObject;
                Undo.RecordObject(go.transform, "RRX Blockout");
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, name);
            }

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
