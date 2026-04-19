using System.Collections.Generic;
using RRX.Environment;
using RRX.Runtime;
using RRX.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using UnityEngine.EventSystems;

namespace RRX.Editor
{
    /// <summary>
    /// XR setup for <b>room-scale / anchored play space</b>: no artificial thumbstick walking or turning.
    /// Position comes from headset + controller tracking (walk in the real room). Controllers still use
    /// <c>Assets/RRX/Input</c> for interaction bindings.
    /// </summary>
    static class RRXDemoSceneWizard
    {
        internal const string InputActionsAssetPath = "Assets/RRX/Input/XRI Default Input Actions.inputactions";

        static readonly string[] XrOriginPrefabCandidates =
        {
            "Packages/com.unity.xr.interaction.toolkit/Samples~/Starter Assets/Prefabs/XR Origin (XR Rig).prefab",
            "Packages/com.unity.xr.interaction.toolkit/Prefabs/XR Origin (XR Rig).prefab",
            "Packages/com.unity.xr.interaction.toolkit/Prefabs/XRI Default XR Rig.prefab",
        };

        const string LocomotionChildName = "RRX_Locomotion";
        const string StripLocomotionChildName = "Locomotion System";

        [MenuItem("RRX/Add Floating HUD To XR Camera", false, 25)]
        [MenuItem("Window/RRX/Add Floating HUD To XR Camera", false, 25)]
        static void MenuAddFloatingHud()
        {
            EnsureFloatingHud();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[RRX] Floating HUD added under XR camera (if origin present). Save the scene.");
        }

        [MenuItem("RRX/Add Mall Crowd (Pedestrians)", false, 27)]
        [MenuItem("Window/RRX/Add Mall Crowd (Pedestrians)", false, 27)]
        static void MenuAddMallCrowd()
        {
            EnsureMallCrowd();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[RRX] Mall crowd added. Save the scene.");
        }

        [MenuItem("RRX/Fix Controller UI (XR Ray → Buttons)", false, 26)]
        [MenuItem("Window/RRX/Fix Controller UI (XR Ray → Buttons)", false, 26)]
        static void MenuFixControllerUi()
        {
            EnsureXrBootstrapOnRig();
            EnsureFloorTrackingForRoomScale();
            EnsureXrUiEventSystemForControllers();
            EnsureRayInteractorManagersLinked();
            EnsureInteractorLineVisualsVisible();
            EnsureXrRayInteractorsActive();
            BindActionReferencesOnControllers();
            EnsureControllerVisuals();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[RRX] XR controllers + UI ray setup refreshed. Save the scene.");
        }

        [MenuItem("RRX/Build Complete MR Scene (Auto)", false, -100)]
        [MenuItem("Window/RRX/Build Complete MR Scene (Auto)", false, -100)]
        static void MenuBuildCompleteMrSceneAuto()
        {
            RunMinimalXrPipeline();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            var origin = Object.FindObjectOfType<XROrigin>();
            if (origin != null)
                Selection.activeGameObject = origin.gameObject;
            Debug.Log("[RRX] Room-scale XR (physical movement only; no stick locomotion). Save the scene (Ctrl/Cmd+S).");
        }

        static void RunMinimalXrPipeline()
        {
            EnsureXrInteractionManager();
            EnsureXrOriginFromPrefabOrMenu();
            DisableStandaloneMainCameraIfXrRigPresent();
            StripPackagedLocomotionSubtreeIfPresent();
            EnsureLocomotionAndCollision();
            DisableStarterTeleportInteractors();
            EnsureXrBootstrapOnRig();
            EnsureFloorTrackingForRoomScale();
            EnsureScenarioGraph();
            RRXAudioMixerBuilder.EnsureMixerAndWireScene();
            RRXCubeBlockoutMenu.RunBlockoutGeneration();
            EnsureFloatingHud();
            EnsureXrUiEventSystemForControllers();
            EnsureRayInteractorManagersLinked();
            EnsureInteractorLineVisualsVisible();
            EnsureXrRayInteractorsActive();
            BindActionReferencesOnControllers();
            EnsureControllerVisuals();
            RRXMrCameraMenu.TryApplyMrCameraHints(showDialogIfNoOrigin: false);
            RRXPatientBuilder.SpawnOrRebuildPatient();
            RRXInteractionsBuilder.BindTriggerHotspots();
            RRXWristPanelBuilder.SpawnOrRebuild();
            RRXScenarioFeedbackBuilder.SpawnOrRebuild();
            EnsureMallCrowd();
            RRXAmbienceBuilder.SpawnOrRebuild();
            RRXAudioMixerBuilder.EnsureMixerAndWireScene();
        }

        static void EnsureScenarioGraph()
        {
            if (Object.FindObjectOfType<ScenarioRunner>() != null)
                return;

            var root = new GameObject("RRX_Scenario");
            Undo.RegisterCreatedObjectUndo(root, "RRX Scenario");
            var clock = Undo.AddComponent<ScenarioClock>(root);
            var runner = Undo.AddComponent<ScenarioRunner>(root);

            var so = new SerializedObject(runner);
            var clockProp = so.FindProperty("_clock");
            if (clockProp != null)
            {
                clockProp.objectReferenceValue = clock;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(runner);
        }

        static void EnsureXrBootstrapOnRig()
        {
            var origin = Object.FindObjectOfType<XROrigin>();
            if (origin == null)
                return;

            if (origin.GetComponent<RRXXrBootstrap>() != null)
                return;

            Undo.RegisterCompleteObjectUndo(origin.gameObject, "RRX XR Bootstrap");
            Undo.AddComponent<RRXXrBootstrap>(origin.gameObject);
        }

        /// <summary>
        /// Floor-relative tracking reduces unwanted vertical drift vs device-relative origins on standalone headsets.
        /// </summary>
        static void EnsureFloorTrackingForRoomScale()
        {
            var origin = Object.FindObjectOfType<XROrigin>();
            if (origin == null)
                return;

            var so = new SerializedObject(origin);
            var p = so.FindProperty("m_RequestedTrackingOriginMode");
            if (p == null)
                return;

            p.enumValueIndex = (int)XROrigin.TrackingOriginMode.Floor;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(origin);
        }

        /// <summary>
        /// Persisted rigs sometimes leave <c>m_InteractionManager</c> null; runtime usually recovers, but linking avoids odd states.
        /// </summary>
        static void EnsureRayInteractorManagersLinked()
        {
            var mgr = Object.FindObjectOfType<XRInteractionManager>();
            var origin = Object.FindObjectOfType<XROrigin>();
            if (mgr == null || origin == null)
                return;

            foreach (var interactor in origin.GetComponentsInChildren<XRBaseInteractor>(true))
            {
                var so = new SerializedObject(interactor);
                var prop = so.FindProperty("m_InteractionManager");
                if (prop == null || prop.objectReferenceValue != null)
                    continue;

                Undo.RecordObject(interactor, "RRX Link XR Interaction Manager");
                prop.objectReferenceValue = mgr;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(interactor);
            }
        }

        /// <summary>
        /// Bright, on-top line renderers so lasers read clearly over MR passthrough.
        /// </summary>
        static void EnsureInteractorLineVisualsVisible()
        {
            var origin = Object.FindObjectOfType<XROrigin>();
            if (origin == null)
                return;

            foreach (var viz in origin.GetComponentsInChildren<XRInteractorLineVisual>(true))
            {
                Undo.RecordObject(viz, "RRX Line visual");
                viz.enabled = true;

                var lr = viz.GetComponent<LineRenderer>();
                if (lr == null)
                    continue;

                Undo.RecordObject(lr, "RRX Line renderer");
                lr.enabled = true;
                lr.sortingOrder = 320;
            }
        }

        /// <summary>
        /// Swap plain Input System UI modules for <see cref="XRUIInputModule"/> so XR ray interactors press world canvases.
        /// </summary>
        static void EnsureXrUiEventSystemForControllers()
        {
            foreach (var es in Object.FindObjectsOfType<EventSystem>())
            {
                var go = es.gameObject;
                foreach (var m in go.GetComponents<StandaloneInputModule>())
                {
                    Undo.RecordObject(go, "RRX XR UI Input");
                    Undo.DestroyObjectImmediate(m);
                }

#if ENABLE_INPUT_SYSTEM
                foreach (var m in go.GetComponents<UnityEngine.InputSystem.UI.InputSystemUIInputModule>())
                {
                    Undo.RecordObject(go, "RRX XR UI Input");
                    Undo.DestroyObjectImmediate(m);
                }
#endif

                var xr = go.GetComponent<XRUIInputModule>();
                if (xr == null)
                {
                    Undo.RecordObject(go, "RRX XR UI Input");
                    xr = Undo.AddComponent<XRUIInputModule>(go);
                }

                Undo.RecordObject(xr, "RRX XR UI Input");
                xr.activeInputMode = XRUIInputModule.ActiveInputMode.InputSystemActions;
                xr.enableXRInput = true;
                EditorUtility.SetDirty(xr);
            }
        }

        /// <summary>
        /// Re-enable XR ray interactors under the rig and force UI interaction so lasers can click
        /// world-space canvases (teleport siblings may be disabled separately).
        /// </summary>
        static void EnsureXrRayInteractorsActive()
        {
            var xrOrigin = Object.FindObjectOfType<XROrigin>();
            if (xrOrigin == null)
                return;

            foreach (var ray in xrOrigin.GetComponentsInChildren<XRRayInteractor>(true))
            {
                Undo.RecordObject(ray, "RRX Enable Ray Interactor");
                ray.enableUIInteraction = true;
                ray.enabled = true;
                EditorUtility.SetDirty(ray);
            }
        }

        static void EnsureFloatingHud()
        {
            var origin = Object.FindObjectOfType<XROrigin>();
            if (origin == null || origin.Camera == null)
                return;

            var cam = origin.Camera.transform;
            if (cam.GetComponentInChildren<RRXFloatingHud>(true) != null)
                return;

            var go = new GameObject("RRX_FloatingHUD");
            Undo.RegisterCreatedObjectUndo(go, "RRX Floating HUD");
            Undo.SetTransformParent(go.transform, cam, "RRX Floating HUD");
            go.transform.localPosition = new Vector3(0f, -0.05f, 2.24f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Undo.AddComponent<RRXFloatingHud>(go);
        }

        static void EnsureMallCrowd()
        {
            if (Object.FindObjectOfType<RRXMallCrowd>() != null)
                return;

            var go = new GameObject("RRX_MallCrowd");
            Undo.RegisterCreatedObjectUndo(go, "RRX Mall Crowd");
            Undo.AddComponent<RRXMallCrowd>(go);
        }

        static GameObject TryFindXrOriginPrefabAsset()
        {
            foreach (var path in XrOriginPrefabCandidates)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<XROrigin>() != null)
                    return prefab;
            }

            foreach (var guid in AssetDatabase.FindAssets("XR Origin (XR Rig) t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab != null && prefab.GetComponent<XROrigin>() != null)
                    return prefab;
            }

            return null;
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
            var existing = Object.FindObjectOfType<XROrigin>();
            if (existing != null)
            {
                ResetXrOriginToWorldZero(existing);
                return;
            }

            var prefab = TryFindXrOriginPrefabAsset();
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "XR Origin");
                instance.name = "XR Origin (XR Rig)";
                var inst = instance.GetComponent<XROrigin>();
                if (inst != null)
                    ResetXrOriginToWorldZero(inst);
                return;
            }

            EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)");
            var spawned = Object.FindObjectOfType<XROrigin>();
            if (spawned == null)
                Debug.LogWarning(
                    "[RRX] Could not spawn XR Origin. Install XR Interaction Toolkit, then GameObject → XR → XR Origin (VR).");
            else
                ResetXrOriginToWorldZero(spawned);
        }

        /// <summary>
        /// Floor-tracking mode treats y=0 as the user's physical floor. Non-zero rig Y makes the camera
        /// appear floating above the play space on start.
        /// </summary>
        static void ResetXrOriginToWorldZero(XROrigin origin)
        {
            var t = origin.transform;
            Undo.RecordObject(t, "RRX Reset XR Origin");
            t.position = Vector3.zero;
            t.rotation = Quaternion.identity;
        }

        static void StripPackagedLocomotionSubtreeIfPresent()
        {
            var xrOrigin = Object.FindObjectOfType<XROrigin>();
            if (xrOrigin == null)
                return;

            var root = xrOrigin.transform;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var ch = root.GetChild(i);
                if (ch.name != StripLocomotionChildName)
                    continue;
                Undo.DestroyObjectImmediate(ch.gameObject);
            }
        }

        static void DisableStarterTeleportInteractors()
        {
            var xrOrigin = Object.FindObjectOfType<XROrigin>();
            if (xrOrigin == null)
                return;

            foreach (var t in xrOrigin.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Teleport Interactor")
                    continue;
                Undo.RecordObject(t.gameObject, "RRX Disable Teleport Interactor");
                t.gameObject.SetActive(false);
            }
        }

        static void EnsureLocomotionAndCollision()
        {
            var xrOrigin = Object.FindObjectOfType<XROrigin>();
            if (xrOrigin == null)
                return;

            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath);
            if (asset == null)
            {
                Debug.LogError($"[RRX] Missing '{InputActionsAssetPath}'.");
                return;
            }

            BindInputActionManagerSingleAsset(xrOrigin.gameObject, asset);
            RemoveCharacterControllerFromXrRig(xrOrigin);
            RemoveArtificialLocomotionUnderRig(xrOrigin);
        }

        static void BindInputActionManagerSingleAsset(GameObject xrOriginRoot, InputActionAsset asset)
        {
            var mam = xrOriginRoot.GetComponent<InputActionManager>() ??
                      Undo.AddComponent<InputActionManager>(xrOriginRoot);
            if (mam == null)
                mam = xrOriginRoot.AddComponent<InputActionManager>();
            if (mam == null)
                return;

            var so = new SerializedObject(mam);
            var prop = so.FindProperty("m_ActionAssets");
            if (prop == null || !prop.isArray)
                return;

            prop.arraySize = 1;
            prop.GetArrayElementAtIndex(0).objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Smooth locomotion used <see cref="CharacterController.Move"/>; room-scale uses tracking only.
        /// </summary>
        static void RemoveCharacterControllerFromXrRig(XROrigin xrOrigin)
        {
            var rigRoot = xrOrigin.gameObject;
            var cc = rigRoot.GetComponent<CharacterController>();
            if (cc != null)
                Undo.DestroyObjectImmediate(cc);

            var rb = rigRoot.GetComponent<Rigidbody>();
            if (rb != null)
                Undo.DestroyObjectImmediate(rb);
        }

        /// <summary>
        /// Removes artificial locomotion (smooth move, snap/smooth turn, teleport drivers, etc.)
        /// so tracking-only room-scale stays anchored to the physical play space.
        /// </summary>
        static void RemoveArtificialLocomotionUnderRig(XROrigin xrOrigin)
        {
            foreach (var lp in xrOrigin.GetComponentsInChildren<LocomotionProvider>(true))
                Undo.DestroyObjectImmediate(lp);

            foreach (var ls in xrOrigin.GetComponentsInChildren<LocomotionSystem>(true))
                Undo.DestroyObjectImmediate(ls);

            var extra = xrOrigin.transform.Find(LocomotionChildName);
            if (extra != null)
                Undo.DestroyObjectImmediate(extra.gameObject);
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
            }
        }

        static readonly string[] LegacyWorldUiObjects =
        {
            "RRX_UI_Root",
            "RRX_EventSystem",
        };

        /// <summary>Deletes leftover world UI / EventSystem objects after removing RRX world UI scripts (save scene after).</summary>
        [MenuItem("RRX/Remove Legacy World UI From Active Scene", false, 45)]
        [MenuItem("Window/RRX/Remove Legacy World UI From Active Scene", false, 45)]
        static void RemoveLegacyWorldUiFromActiveScene()
        {
            foreach (var objectName in LegacyWorldUiObjects)
            {
                var found = new List<GameObject>();
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    CollectTransformsNamed(root.transform, objectName, found);

                foreach (var go in found)
                {
                    if (go != null)
                        Undo.DestroyObjectImmediate(go);
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[RRX] Removed legacy world UI objects (RRX_UI_Root, RRX_EventSystem) where present. Save the scene.");
        }

        static void CollectTransformsNamed(Transform t, string objectName, List<GameObject> append)
        {
            if (t.name == objectName)
                append.Add(t.gameObject);
            for (var i = 0; i < t.childCount; i++)
                CollectTransformsNamed(t.GetChild(i), objectName, append);
        }

        /// <summary>
        /// Looks up each <see cref="InputActionReference"/> sub-asset of the XRI Default Input Actions
        /// asset by its "Map/Action" name and wires them into every <c>ActionBasedController</c> so
        /// Position / Rotation / Select / UI Press actually have bindings.
        /// </summary>
        static void BindActionReferencesOnControllers()
        {
            var origin = Object.FindObjectOfType<XROrigin>();
            if (origin == null)
                return;

            var refsByName = LoadInputActionReferencesByName(InputActionsAssetPath);
            if (refsByName == null || refsByName.Count == 0)
                return;

            foreach (var ctl in origin.GetComponentsInChildren<ActionBasedController>(true))
                BindSingleControllerActions(ctl, refsByName);
        }

        static Dictionary<string, InputActionReference> LoadInputActionReferencesByName(string path)
        {
            var map = new Dictionary<string, InputActionReference>();
            var subs = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var o in subs)
            {
                if (o is InputActionReference r && r.action != null)
                {
                    var key = $"{r.action.actionMap?.name}/{r.action.name}";
                    map[key] = r;
                }
            }
            return map;
        }

        static readonly (string field, string left, string right)[] ControllerActionBindings =
        {
            ("m_PositionAction",        "XRI LeftHand/Position",                "XRI RightHand/Position"),
            ("m_RotationAction",        "XRI LeftHand/Rotation",                "XRI RightHand/Rotation"),
            ("m_IsTrackedAction",       "XRI LeftHand/Is Tracked",              "XRI RightHand/Is Tracked"),
            ("m_TrackingStateAction",   "XRI LeftHand/Tracking State",          "XRI RightHand/Tracking State"),
            ("m_SelectAction",          "XRI LeftHand Interaction/Select",      "XRI RightHand Interaction/Select"),
            ("m_SelectActionValue",     "XRI LeftHand Interaction/Select Value","XRI RightHand Interaction/Select Value"),
            ("m_ActivateAction",        "XRI LeftHand Interaction/Activate",    "XRI RightHand Interaction/Activate"),
            ("m_ActivateActionValue",   "XRI LeftHand Interaction/Activate Value","XRI RightHand Interaction/Activate Value"),
            ("m_UIPressAction",         "XRI LeftHand Interaction/UI Press",    "XRI RightHand Interaction/UI Press"),
            ("m_UIPressActionValue",    "XRI LeftHand Interaction/UI Press Value","XRI RightHand Interaction/UI Press Value"),
            ("m_HapticDeviceAction",    "XRI LeftHand/Haptic Device",           "XRI RightHand/Haptic Device"),
            ("m_RotateAnchorAction",    "XRI LeftHand Interaction/Rotate Anchor","XRI RightHand Interaction/Rotate Anchor"),
            ("m_TranslateAnchorAction", "XRI LeftHand Interaction/Translate Anchor","XRI RightHand Interaction/Translate Anchor"),
        };

        static void BindSingleControllerActions(ActionBasedController ctl,
            Dictionary<string, InputActionReference> refsByName)
        {
            var isLeft = ctl.gameObject.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0;

            var so = new SerializedObject(ctl);
            var changed = false;

            foreach (var (field, left, right) in ControllerActionBindings)
            {
                var wantName = isLeft ? left : right;
                if (!refsByName.TryGetValue(wantName, out var reference) || reference == null)
                    continue;

                var prop = so.FindProperty(field);
                if (prop == null)
                    continue;

                var useRef = prop.FindPropertyRelative("m_UseReference");
                var refProp = prop.FindPropertyRelative("m_Reference");
                if (useRef == null || refProp == null)
                    continue;

                if (!useRef.boolValue || refProp.objectReferenceValue != reference)
                {
                    useRef.boolValue = true;
                    refProp.objectReferenceValue = reference;
                    changed = true;
                }
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ctl);
            }
        }

        /// <summary>
        /// The Starter prefab ships no controller mesh; add a simple primitive visual so the user can
        /// see each hand's position in the scene until a bespoke model is plugged in.
        /// </summary>
        static void EnsureControllerVisuals()
        {
            var origin = Object.FindObjectOfType<XROrigin>();
            if (origin == null)
                return;

            foreach (var ctl in origin.GetComponentsInChildren<ActionBasedController>(true))
            {
                var existing = ctl.transform.Find("RRX_ControllerModel");
                if (existing != null)
                    continue;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(go, "RRX Controller Visual");
                go.name = "RRX_ControllerModel";
                Undo.SetTransformParent(go.transform, ctl.transform, "RRX Controller Visual");
                go.transform.localPosition = new Vector3(0f, -0.005f, 0.04f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = new Vector3(0.04f, 0.025f, 0.1f);

                var collider = go.GetComponent<Collider>();
                if (collider != null)
                    Undo.DestroyObjectImmediate(collider);

                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (shader != null)
                    {
                        var mat = new Material(shader) { color = new Color(0.12f, 0.75f, 1f, 1f) };
                        mr.sharedMaterial = mat;
                    }
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
            }
        }

    }
}
