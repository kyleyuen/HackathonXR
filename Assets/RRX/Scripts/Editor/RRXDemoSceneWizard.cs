using System.Collections.Generic;
using System.Linq;
using RRX.Core;
using RRX.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

namespace RRX.Editor
{
    /// <summary>
    /// One-click RRX setup in Unity: XR rig, scenario runner, zones, grab props.
    /// Menu: <b>RRX → Setup Demo In Active Scene</b> or <b>New RRX_Overdose Scene And Setup</b>.
    /// </summary>
    static class RRXDemoSceneWizard
    {
        const string PrefabPathPrimary =
            "Packages/com.unity.xr.interaction.toolkit/Prefabs/XR Origin (XR Rig).prefab";

        const string PrefabPathAlt =
            "Packages/com.unity.xr.interaction.toolkit/Prefabs/XRI Default XR Rig.prefab";

        /// <summary>One menu: XR rig + scenario + zones + cube MR room + passthrough-ready cameras.</summary>
        [MenuItem("RRX/Build Complete MR Scene (Auto)", false, -100)]
        [MenuItem("Window/RRX/Build Complete MR Scene (Auto)", false, -100)]
        static void MenuBuildCompleteMrSceneAuto()
        {
            RunFullMrDemoPipeline();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = GameObject.Find("RRX_Scenario");
            Debug.Log(
                "[RRX] Full auto-build finished: demo + MR blockout + camera hints. Save the scene (Ctrl/Cmd+S).");
        }

        [MenuItem("RRX/Setup Demo In Active Scene", false, 0)]
        [MenuItem("Window/RRX/Setup Demo In Active Scene", false, 10)]
        static void MenuSetupDemo()
        {
            SetupDemoInternal();
        }

        [MenuItem("RRX/New RRX_Overdose Scene And Setup", false, 11)]
        [MenuItem("Window/RRX/New RRX_Overdose Scene And Setup", false, 20)]
        static void MenuNewSceneAndSetup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/RRX/Scenes"))
                AssetDatabase.CreateFolder("Assets/RRX", "Scenes");

            const string scenePath = "Assets/RRX/Scenes/RRX_Overdose.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);

            AddSceneToBuildSettings(scenePath);

            RunFullMrDemoPipeline();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.Refresh();
            Debug.Log($"[RRX] Saved and configured {scenePath} (full MR demo pipeline).");
        }

        /// <summary>Scenario wiring + cube blockout + MR camera hints (same as <see cref="MenuBuildCompleteMrSceneAuto"/>).</summary>
        static void RunFullMrDemoPipeline()
        {
            SetupDemoInternal();
            RRXCubeBlockoutMenu.RunBlockoutGeneration();
            RRXMrCameraMenu.TryApplyMrCameraHints(showDialogIfNoOrigin: false);
        }

        static void SetupDemoInternal()
        {
            EnsureXrInteractionManager();
            EnsureXrOriginFromPrefabOrMenu();
            DisableStandaloneMainCameraIfXrRigPresent();

            var scenario = EnsureGameObject("RRX_Scenario");
            var runner = scenario.GetComponent<ScenarioRunner>() ?? Undo.AddComponent<ScenarioRunner>(scenario);
            var hotkeys = scenario.GetComponent<ScenarioDebugHotkeys>() ??
                          Undo.AddComponent<ScenarioDebugHotkeys>(scenario);

            var patient = EnsureGameObject("RRX_Patient");
            patient.transform.position = new Vector3(0f, 0f, 3f);
            var presenter = patient.GetComponent<PatientPresenter>() ?? Undo.AddComponent<PatientPresenter>(patient);

            EnsurePatientAnimator(patient, presenter);

            BindSerialized(runner, "_patient", presenter);
            BindSerialized(hotkeys, "_runner", runner);

            BuildInteractionZone(runner);
            BuildGrabCube(runner, "RRX_Prop_Phone", new Vector3(0.45f, 0.9f, 2.6f),
                new Vector3(0.12f, 0.03f, 0.06f), ScenarioAction.Call911);
            BuildGrabCube(runner, "RRX_Prop_Narcan", new Vector3(-0.45f, 0.9f, 2.6f),
                new Vector3(0.08f, 0.04f, 0.04f), ScenarioAction.AdministerNarcan);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = scenario;

            Debug.Log(
                "[RRX] Demo wiring complete — save the scene (Ctrl/Cmd+S). Play: keys 1 / 2 / 3 / R or VR grab Phone & Narcan.");
        }

        static void EnsurePatientAnimator(GameObject patient, PatientPresenter presenter)
        {
            Transform visualTransform = patient.transform.Find("Visual");
            GameObject visualGo;
            if (visualTransform == null)
            {
                visualGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visualGo.name = "Visual";
                visualGo.transform.SetParent(patient.transform, false);
                visualGo.transform.localPosition = new Vector3(0f, 1f, 0f);
                Undo.RegisterCreatedObjectUndo(visualGo, "RRX Patient Visual");
            }
            else
                visualGo = visualTransform.gameObject;

            var animator = visualGo.GetComponent<Animator>() ?? Undo.AddComponent<Animator>(visualGo);

            var so = new SerializedObject(presenter);
            var prop = so.FindProperty("CharacterAnimator");
            if (prop != null)
            {
                prop.objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void BindSerialized(Component c, string fieldName, Object value)
        {
            var so = new SerializedObject(c);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[RRX] No field '{fieldName}' on {c.GetType().Name}");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetEnum(Component c, string fieldName, int enumIndex)
        {
            var so = new SerializedObject(c);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.enumValueIndex = enumIndex;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void BuildInteractionZone(ScenarioRunner runner)
        {
            var go = EnsureGameObject("RRX_Zone_CheckResponsiveness");
            go.transform.position = new Vector3(0f, 1f, 3f);

            // RequireComponent(Collider) on ScenarioTriggerAction can add a non-Box collider; always ensure BoxCollider.
            var box = go.GetComponent<BoxCollider>();
            if (box == null)
                box = Undo.AddComponent<BoxCollider>(go);
            if (box == null)
                box = go.AddComponent<BoxCollider>();
            if (box == null)
            {
                Debug.LogError("[RRX] Could not add BoxCollider to RRX_Zone_CheckResponsiveness.");
                return;
            }

            box.isTrigger = true;
            box.size = new Vector3(1f, 1.5f, 1f);

            var tri = go.GetComponent<ScenarioTriggerAction>();
            if (tri == null)
                tri = Undo.AddComponent<ScenarioTriggerAction>(go);
            if (tri == null)
                tri = go.AddComponent<ScenarioTriggerAction>();
            if (tri == null)
            {
                Debug.LogError("[RRX] Could not add ScenarioTriggerAction to zone.");
                return;
            }

            BindSerialized(tri, "_runner", runner);
            SetEnum(tri, "_action", (int)ScenarioAction.CheckResponsiveness);
        }

        static void BuildGrabCube(ScenarioRunner runner, string objectName, Vector3 worldPos, Vector3 localScale,
            ScenarioAction action)
        {
            var go = GameObject.Find(objectName);
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = objectName;
                Undo.RegisterCreatedObjectUndo(go, objectName);
            }

            go.transform.position = worldPos;
            go.transform.localScale = localScale;

            // XRGrabInteractable expects a Rigidbody; add it before XRI enables.
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                rb = Undo.AddComponent<Rigidbody>(go);
            if (rb == null)
                rb = go.AddComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError($"[RRX] Could not add Rigidbody to {objectName}; grab setup skipped.");
                return;
            }

            rb.isKinematic = true;
            rb.useGravity = false;

            if (go.GetComponent<XRGrabInteractable>() == null)
                Undo.AddComponent<XRGrabInteractable>(go);

            var relay = go.GetComponent<ScenarioXRSelectAction>() ?? Undo.AddComponent<ScenarioXRSelectAction>(go);
            BindSerialized(relay, "_runner", runner);
            SetEnum(relay, "_action", (int)action);
        }

        static GameObject EnsureGameObject(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        static void EnsureXrInteractionManager()
        {
            if (Object.FindObjectOfType<XRInteractionManager>() != null)
                return;

            var go = new GameObject("XR Interaction Manager");
            Undo.RegisterCreatedObjectUndo(go, "XR Interaction Manager");
            Undo.AddComponent<XRInteractionManager>(go);
        }

        static void EnsureXrOriginFromPrefabOrMenu()
        {
            if (Object.FindObjectOfType<XROrigin>() != null)
                return;

            var prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPathPrimary) ??
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPathAlt);

            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "XR Origin");
                instance.name = "XR Origin (XR Rig)";
                Debug.Log("[RRX] Instantiated XR Origin prefab from XR Interaction Toolkit.");
                return;
            }

            EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)");
            if (Object.FindObjectOfType<XROrigin>() == null)
                Debug.LogWarning(
                    "[RRX] Could not spawn XR Origin. Install XR Interaction Toolkit, then GameObject → XR → XR Origin (VR).");
        }

        static void DisableStandaloneMainCameraIfXrRigPresent()
        {
            if (Object.FindObjectOfType<XROrigin>() == null)
                return;

            var mains = GameObject.FindGameObjectsWithTag("MainCamera");
            foreach (var cam in mains)
            {
                if (cam.GetComponentInParent<XROrigin>() != null)
                    continue;
                Undo.RecordObject(cam, "Disable standalone Main Camera");
                cam.SetActive(false);
                Debug.Log("[RRX] Disabled standalone Main Camera (XR rig provides camera).");
            }
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Any(s => s.path == scenePath))
                return;

            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
