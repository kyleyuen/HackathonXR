using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using Unity.XR.CoreUtils;

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
            RRXCubeBlockoutMenu.RunBlockoutGeneration();
            RRXMrCameraMenu.TryApplyMrCameraHints(showDialogIfNoOrigin: false);
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
            if (Object.FindObjectOfType<XROrigin>() != null)
                return;

            var prefab = TryFindXrOriginPrefabAsset();
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "XR Origin");
                instance.name = "XR Origin (XR Rig)";
                return;
            }

            EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)");
            if (Object.FindObjectOfType<XROrigin>() == null)
                Debug.LogWarning(
                    "[RRX] Could not spawn XR Origin. Install XR Interaction Toolkit, then GameObject → XR → XR Origin (VR).");
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

    }
}
