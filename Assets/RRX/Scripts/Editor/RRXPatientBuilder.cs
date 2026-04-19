using RRX.Core;
using RRX.Interactions;
using RRX.Runtime;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace RRX.Editor
{
    /// <summary>
    /// Spawns a blockout patient lying supine on the MR floor in front of the user so the player can kneel
    /// / crouch next to them and "operate" — check responsiveness on the shoulder, call 911 on the phone,
    /// and press the nasal-narcan hotspot. Wires up <see cref="PatientPresenter"/> to the scene's
    /// <see cref="ScenarioRunner"/> automatically.
    /// </summary>
    static class RRXPatientBuilder
    {
        internal const string RootName = "RRX_Patient";
        const string MatFolder = "Assets/RRX/Materials";

        // Patient is placed in the XR rig's local forward: head close to the user, body extending away
        // so when the user walks forward they approach the patient's head / chest / shoulders.
        const float HeadLocalZ = 0.85f;
        const float TorsoLocalZ = 1.30f;
        const float PelvisLocalZ = 1.70f;
        const float ThighLocalZ = 2.00f;
        const float ShinLocalZ = 2.40f;
        const float FootLocalZ = 2.62f;

        [MenuItem("RRX/Spawn Patient In Front Of User", false, 45)]
        [MenuItem("Window/RRX/Spawn Patient In Front Of User", false, 45)]
        static void MenuSpawn()
        {
            var go = SpawnOrRebuildPatient();
            if (go != null)
                Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[RRX] Patient + interaction hotspots spawned 1.3m in front of the XR rig. Save the scene.");
        }

        /// <summary>Called by the demo scene wizard so the auto build pipeline always spawns a patient.</summary>
        public static GameObject SpawnOrRebuildPatient()
        {
            var origin = Object.FindObjectOfType<XROrigin>();
            Vector3 rigPos = origin != null ? origin.transform.position : Vector3.zero;
            Quaternion rigRot = origin != null ? origin.transform.rotation : Quaternion.identity;

            var existing = GameObject.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "RRX Patient");
            root.transform.SetPositionAndRotation(rigPos, rigRot);

            var skin = GetOrCreateMat("RRX_Mat_PatientSkin", new Color(0.82f, 0.70f, 0.62f));
            var shirt = GetOrCreateMat("RRX_Mat_PatientShirt", new Color(0.35f, 0.48f, 0.60f));
            var pants = GetOrCreateMat("RRX_Mat_PatientPants", new Color(0.20f, 0.24f, 0.30f));
            var hair = GetOrCreateMat("RRX_Mat_PatientHair", new Color(0.12f, 0.10f, 0.09f));
            var shoe = GetOrCreateMat("RRX_Mat_PatientShoe", new Color(0.08f, 0.08f, 0.09f));
            var phoneMat = GetOrCreateMat("RRX_Mat_Phone", new Color(0.05f, 0.08f, 0.12f));
            var hotspotMat = GetOrCreateMat("RRX_Mat_PatientHotspot", new Color(0.95f, 0.35f, 0.35f, 0.55f));
            ApplyTransparent(hotspotMat);

            BuildBody(root.transform, skin, shirt, pants, hair, shoe);

            var presenter = root.AddComponent<PatientPresenter>();
            Undo.RegisterCreatedObjectUndo(presenter, "RRX Patient Presenter");
            var procedural = root.AddComponent<RRXPatientProceduralVisuals>();
            Undo.RegisterCreatedObjectUndo(procedural, "RRX Patient Procedural Visuals");

            var runner = Object.FindObjectOfType<ScenarioRunner>();
            WirePresenterToRunner(runner, presenter);

            BuildHotspot(root.transform, "Hotspot_SceneScan",
                localCenter: new Vector3(0f, 0.35f, TorsoLocalZ),
                localSize: new Vector3(1.8f, 1.0f, 2.4f),
                material: null,          // invisible — no mesh renderer
                hotspotId: ScenarioHotspotId.SceneScan,
                runner: runner,
                visible: false);

            BuildHotspot(root.transform, "Hotspot_Shoulder",
                localCenter: new Vector3(-0.19f, 0.26f, TorsoLocalZ - 0.22f),
                localSize: new Vector3(0.20f, 0.20f, 0.20f),
                material: hotspotMat,
                hotspotId: ScenarioHotspotId.Shoulder,
                runner: runner);

            BuildHotspot(root.transform, "Hotspot_Chin",
                localCenter: new Vector3(0f, 0.12f, HeadLocalZ + 0.12f),
                localSize: new Vector3(0.16f, 0.12f, 0.14f),
                material: hotspotMat,
                hotspotId: ScenarioHotspotId.Chin,
                runner: runner);

            BuildHotspot(root.transform, "Hotspot_Mouth",
                localCenter: new Vector3(0f, 0.16f, HeadLocalZ - 0.04f),
                localSize: new Vector3(0.16f, 0.12f, 0.14f),
                material: hotspotMat,
                hotspotId: ScenarioHotspotId.Mouth,
                runner: runner);

            BuildHotspot(root.transform, "Hotspot_NasalNarcan",
                localCenter: new Vector3(0f, 0.30f, HeadLocalZ - 0.02f),
                localSize: new Vector3(0.18f, 0.14f, 0.14f),
                material: hotspotMat,
                hotspotId: ScenarioHotspotId.Nose,
                runner: runner);

            BuildHotspot(root.transform, "Hotspot_Hip",
                localCenter: new Vector3(0.15f, 0.14f, PelvisLocalZ),
                localSize: new Vector3(0.22f, 0.20f, 0.20f),
                material: hotspotMat,
                hotspotId: ScenarioHotspotId.Hip,
                runner: runner);

            BuildPhone(root.transform, phoneMat, runner);
            BuildDecoys(root.transform, runner);

            return root;
        }

        static void BuildBody(Transform root, Material skin, Material shirt, Material pants, Material hair,
            Material shoe)
        {
            BuildCube(root, "Torso", shirt,
                new Vector3(0f, 0.13f, TorsoLocalZ),
                new Vector3(0.42f, 0.22f, 0.58f));
            BuildCube(root, "Pelvis", pants,
                new Vector3(0f, 0.12f, PelvisLocalZ),
                new Vector3(0.38f, 0.20f, 0.18f));

            BuildCube(root, "ThighLeft", pants,
                new Vector3(-0.10f, 0.12f, ThighLocalZ),
                new Vector3(0.14f, 0.18f, 0.40f));
            BuildCube(root, "ShinLeft", pants,
                new Vector3(-0.10f, 0.10f, ShinLocalZ),
                new Vector3(0.12f, 0.14f, 0.40f));
            BuildCube(root, "ShoeLeft", shoe,
                new Vector3(-0.10f, 0.08f, FootLocalZ),
                new Vector3(0.12f, 0.10f, 0.20f));

            BuildCube(root, "ThighRight", pants,
                new Vector3(0.10f, 0.12f, ThighLocalZ),
                new Vector3(0.14f, 0.18f, 0.40f));
            BuildCube(root, "ShinRight", pants,
                new Vector3(0.10f, 0.10f, ShinLocalZ),
                new Vector3(0.12f, 0.14f, 0.40f));
            BuildCube(root, "ShoeRight", shoe,
                new Vector3(0.10f, 0.08f, FootLocalZ),
                new Vector3(0.12f, 0.10f, 0.20f));

            BuildCube(root, "UpperArmLeft", shirt,
                new Vector3(-0.28f, 0.13f, TorsoLocalZ - 0.06f),
                new Vector3(0.10f, 0.12f, 0.42f));
            BuildCube(root, "ForearmLeft", skin,
                new Vector3(-0.30f, 0.10f, TorsoLocalZ + 0.30f),
                new Vector3(0.08f, 0.10f, 0.36f));
            BuildCube(root, "HandLeft", skin,
                new Vector3(-0.30f, 0.10f, TorsoLocalZ + 0.52f),
                new Vector3(0.08f, 0.05f, 0.12f));

            BuildCube(root, "UpperArmRight", shirt,
                new Vector3(0.28f, 0.13f, TorsoLocalZ - 0.06f),
                new Vector3(0.10f, 0.12f, 0.42f));
            BuildCube(root, "ForearmRight", skin,
                new Vector3(0.30f, 0.10f, TorsoLocalZ + 0.30f),
                new Vector3(0.08f, 0.10f, 0.36f));
            BuildCube(root, "HandRight", skin,
                new Vector3(0.30f, 0.10f, TorsoLocalZ + 0.52f),
                new Vector3(0.08f, 0.05f, 0.12f));

            BuildCube(root, "Neck", skin,
                new Vector3(0f, 0.15f, HeadLocalZ + 0.14f),
                new Vector3(0.11f, 0.10f, 0.08f));
            BuildSphere(root, "Head", skin,
                new Vector3(0f, 0.18f, HeadLocalZ),
                0.12f);
            BuildCube(root, "Hair", hair,
                new Vector3(0f, 0.25f, HeadLocalZ - 0.04f),
                new Vector3(0.20f, 0.06f, 0.24f));
        }

        static GameObject BuildCube(Transform parent, string name, Material mat, Vector3 localPos,
            Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            ApplyMat(go, mat);
            return go;
        }

        static GameObject BuildSphere(Transform parent, string name, Material mat, Vector3 localPos,
            float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * (radius * 2f);
            ApplyMat(go, mat);
            return go;
        }

        static void BuildHotspot(Transform parent, string name, Vector3 localCenter, Vector3 localSize,
            Material material, ScenarioHotspotId hotspotId, ScenarioRunner runner, bool visible = true)
        {
            GameObject go;
            if (visible)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, name);
                go.transform.localPosition = localCenter;
                go.transform.localScale = localSize;
                if (material != null)
                    ApplyMat(go, material);
                else
                    Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, name);
                go.transform.localPosition = localCenter;
                go.transform.localScale = localSize;
                var col = Undo.AddComponent<BoxCollider>(go);
                col.isTrigger = false;

                var interactable = Undo.AddComponent<XRSimpleInteractable>(go);
                interactable.colliders.Clear();
                interactable.colliders.Add(col);

                var tag = Undo.AddComponent<RRXScenarioHotspotTag>(go);
                SetHotspotId(tag, hotspotId);
                var trigger = Undo.AddComponent<RRXTriggerActivatedHotspot>(go);
                trigger.SetRunner(runner);
                trigger.SetHotspotTag(tag);
                // SceneScan deactivates after scene is scanned
                trigger.SetDisableAfterUse(true);
                Undo.AddComponent<RRXHotspotHighlight>(go);
                return;
            }

            var col2 = go.GetComponent<BoxCollider>();
            if (col2 != null)
                col2.isTrigger = false;

            var interactable2 = Undo.AddComponent<XRSimpleInteractable>(go);
            interactable2.colliders.Clear();
            if (col2 != null)
                interactable2.colliders.Add(col2);

            var tag2 = Undo.AddComponent<RRXScenarioHotspotTag>(go);
            SetHotspotId(tag2, hotspotId);
            var trigger2 = Undo.AddComponent<RRXTriggerActivatedHotspot>(go);
            trigger2.SetRunner(runner);
            trigger2.SetHotspotTag(tag2);
            Undo.AddComponent<RRXHotspotHighlight>(go);
        }

        static void BuildDecoys(Transform root, ScenarioRunner runner)
        {
            var pillMat = GetOrCreateMat("RRX_Mat_DecoyPill", new Color(0.85f, 0.72f, 0.12f, 0.75f));
            ApplyTransparent(pillMat);
            var syringeMat = GetOrCreateMat("RRX_Mat_DecoySyringe", new Color(0.88f, 0.92f, 0.95f, 0.65f));
            ApplyTransparent(syringeMat);
            var waterMat = GetOrCreateMat("RRX_Mat_DecoyWater", new Color(0.22f, 0.55f, 0.90f, 0.65f));
            ApplyTransparent(waterMat);

            BuildDecoy(root, "Decoy_PillBottle",
                localPos: new Vector3(-0.55f, 0.06f, TorsoLocalZ + 0.15f),
                localScale: new Vector3(0.07f, 0.12f, 0.07f),
                mat: pillMat,
                label: "Pills",
                runner: runner);

            BuildDecoy(root, "Decoy_Syringe",
                localPos: new Vector3(0.50f, 0.04f, TorsoLocalZ + 0.30f),
                localScale: new Vector3(0.04f, 0.04f, 0.18f),
                mat: syringeMat,
                label: "Syringe",
                runner: runner);

            BuildDecoy(root, "Decoy_WaterBottle",
                localPos: new Vector3(-0.48f, 0.08f, PelvisLocalZ + 0.10f),
                localScale: new Vector3(0.07f, 0.15f, 0.07f),
                mat: waterMat,
                label: "Water",
                runner: runner);
        }

        static void BuildDecoy(Transform root, string name, Vector3 localPos, Vector3 localScale,
            Material mat, string label, ScenarioRunner runner)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            ApplyMat(go, mat);

            var col = go.GetComponent<CapsuleCollider>();
            if (col != null)
                col.isTrigger = false;

            var interactable = Undo.AddComponent<XRSimpleInteractable>(go);
            interactable.colliders.Clear();
            if (col != null)
                interactable.colliders.Add(col);

            var tag = Undo.AddComponent<RRXScenarioHotspotTag>(go);
            SetHotspotId(tag, ScenarioHotspotId.None);  // Wrong action → failure escalation
            var trigger = Undo.AddComponent<RRXTriggerActivatedHotspot>(go);
            trigger.SetRunner(runner);
            trigger.SetHotspotTag(tag);
            trigger.SetDisableAfterUse(false);  // Decoys stay active as persistent traps

            // Small label above decoy
            var labelGO = new GameObject("Label");
            Undo.RegisterCreatedObjectUndo(labelGO, "Label");
            labelGO.transform.SetParent(go.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelGO.transform.localRotation = Quaternion.identity;
            var mesh = Undo.AddComponent<TextMesh>(labelGO);
            mesh.text = label;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.characterSize = 0.08f;
            mesh.fontSize = 24;
            mesh.color = new Color(1f, 0.8f, 0.2f);
        }

        static void BuildPhone(Transform root, Material phoneMat, ScenarioRunner runner)
        {
            var phone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            phone.name = "RRX_Patient_Phone";
            phone.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(phone, phone.name);
            phone.transform.localPosition = new Vector3(0.42f, 0.04f, 0.95f);
            phone.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
            phone.transform.localScale = new Vector3(0.08f, 0.018f, 0.16f);
            ApplyMat(phone, phoneMat);

            var col = phone.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.isTrigger = false;
                col.size = new Vector3(1.8f, 4.5f, 1.8f); // Easier ray selection without changing visible mesh.
                col.center = new Vector3(0f, 0.9f, 0f);
            }

            var interactable = Undo.AddComponent<XRSimpleInteractable>(phone);
            interactable.colliders.Clear();
            if (col != null)
                interactable.colliders.Add(col);

            var tag = Undo.AddComponent<RRXScenarioHotspotTag>(phone);
            SetHotspotId(tag, ScenarioHotspotId.Phone);
            var trigger = Undo.AddComponent<RRXTriggerActivatedHotspot>(phone);
            trigger.SetRunner(runner);
            trigger.SetHotspotTag(tag);
            Undo.AddComponent<RRXHotspotHighlight>(phone);
            BuildPhoneLabel(phone.transform);
        }

        static void BuildPhoneLabel(Transform parent)
        {
            var label = new GameObject("RRX_PhoneLabel");
            Undo.RegisterCreatedObjectUndo(label, label.name);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            label.transform.localRotation = Quaternion.identity;
            var mesh = Undo.AddComponent<TextMesh>(label);
            mesh.text = "Call 911";
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.characterSize = 0.05f;
            mesh.fontSize = 36;
            mesh.color = Color.white;
        }

        static void WirePresenterToRunner(ScenarioRunner runner, PatientPresenter presenter)
        {
            if (runner == null || presenter == null)
                return;
            var so = new SerializedObject(runner);
            var prop = so.FindProperty("_patient");
            if (prop == null)
                return;
            prop.objectReferenceValue = presenter;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(runner);
        }

        static void ApplyMat(GameObject go, Material mat)
        {
            if (mat == null)
                return;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null)
                r.sharedMaterial = mat;
        }

        static Material GetOrCreateMat(string assetName, Color color)
        {
            var path = $"{MatFolder}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            if (mat.HasProperty("_Color")) mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            EnsureMaterialFolder();
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Upgrades a material to alpha-blend transparency so hotspot cubes show as tinted overlays.</summary>
        static void ApplyTransparent(Material mat)
        {
            if (mat == null) return;
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3f);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
        }

        static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder(MatFolder))
            {
                var parent = "Assets/RRX";
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder("Assets", "RRX");
                AssetDatabase.CreateFolder(parent, "Materials");
            }
        }

        static void SetHotspotId(RRXScenarioHotspotTag tag, ScenarioHotspotId id)
        {
            var so = new SerializedObject(tag);
            var p = so.FindProperty("_hotspotId");
            if (p != null)
            {
                p.enumValueIndex = (int)id;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tag);
            }
        }
    }
}
