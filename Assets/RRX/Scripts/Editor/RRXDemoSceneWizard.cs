using System.Collections.Generic;
using RRX.Locomotion;
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
    /// Minimal XR setup: interaction manager, XR Origin, CharacterController,
    /// tank move (left stick Y) + smooth yaw (right stick X) using <c>Assets/RRX/Input</c>.
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
            Debug.Log("[RRX] Minimal XR (walk + turn) ready. Save the scene (Ctrl/Cmd+S).");
        }

        static void RunMinimalXrPipeline()
        {
            EnsureXrInteractionManager();
            EnsureXrOriginFromPrefabOrMenu();
            DisableStandaloneMainCameraIfXrRigPresent();
            StripPackagedLocomotionSubtreeIfPresent();
            EnsureLocomotionAndCollision();
            DisableStarterTeleportInteractors();
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
            EnsureCharacterController(xrOrigin);
            EnsureLocomotionComponents(xrOrigin, asset);
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

        static void EnsureCharacterController(XROrigin xrOrigin)
        {
            var rigRoot = xrOrigin.gameObject;

            var xrSo = new SerializedObject(xrOrigin);
            var originProp = xrSo.FindProperty("m_OriginBaseGameObject");
            if (originProp != null && originProp.objectReferenceValue == null)
            {
                originProp.objectReferenceValue = rigRoot;
                xrSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var rb = rigRoot.GetComponent<Rigidbody>();
            if (rb != null)
                Undo.DestroyObjectImmediate(rb);

            CharacterController cc = rigRoot.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = Undo.AddComponent<CharacterController>(rigRoot);
                if (cc == null)
                    cc = rigRoot.AddComponent<CharacterController>();
            }

            if (cc == null)
                return;

            Undo.RecordObject(cc, "RRX Character Controller");

            xrSo.Update();
            var yProp = xrSo.FindProperty("m_CameraYOffset");
            var eyeHeight = yProp != null ? yProp.floatValue : 1.36144f;

            var height = Mathf.Clamp(eyeHeight + 0.35f, 1.15f, 2.05f);
            cc.height = height;
            cc.radius = 0.18f;
            cc.center = new Vector3(0f, height * 0.5f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.08f;
            cc.minMoveDistance = 0f;
            cc.detectCollisions = true;
            cc.enableOverlapRecovery = true;
        }

        static void EnsureLocomotionComponents(XROrigin xrOrigin, InputActionAsset asset)
        {
            var locomotionGo = EnsureLocomotionGameObject(xrOrigin);

            var locomotionSystem = locomotionGo.GetComponent<LocomotionSystem>() ??
                                   Undo.AddComponent<LocomotionSystem>(locomotionGo);
            if (locomotionSystem == null)
                locomotionSystem = locomotionGo.AddComponent<LocomotionSystem>();
            if (locomotionSystem == null)
                return;

            var sysSo = new SerializedObject(locomotionSystem);
            var lsOrigin = sysSo.FindProperty("m_XROrigin");
            if (lsOrigin != null)
                lsOrigin.objectReferenceValue = xrOrigin;
            sysSo.ApplyModifiedPropertiesWithoutUndo();

            var leftMove = asset.FindAction("XRI LeftHand Locomotion/Move");
            var rightMove = asset.FindAction("XRI RightHand Locomotion/Move");

            if (leftMove == null || rightMove == null)
            {
                Debug.LogError("[RRX] Locomotion Move actions missing in Input Action Asset.");
                return;
            }

            foreach (var legacy in locomotionGo.GetComponents<ActionBasedContinuousMoveProvider>())
                Undo.DestroyObjectImmediate(legacy);
            foreach (var legacy in locomotionGo.GetComponents<ActionBasedSnapTurnProvider>())
                Undo.DestroyObjectImmediate(legacy);
            foreach (var legacy in locomotionGo.GetComponents<ActionBasedContinuousTurnProvider>())
                Undo.DestroyObjectImmediate(legacy);
            foreach (var legacy in locomotionGo.GetComponents<RRXTankForwardMoveProvider>())
                Undo.DestroyObjectImmediate(legacy);
            foreach (var legacy in locomotionGo.GetComponents<RRXTankYawTurnProvider>())
                Undo.DestroyObjectImmediate(legacy);

            var move = Undo.AddComponent<RRXTankForwardMoveProvider>(locomotionGo);
            if (move == null)
                move = locomotionGo.AddComponent<RRXTankForwardMoveProvider>();
            if (move == null)
                return;

            Undo.RecordObject(move, "RRX Tank Move");
            move.system = locomotionSystem;
            move.moveSpeed = 2.75f;
            move.enableStrafe = false;
            move.enableFly = false;
            move.useGravity = true;
            move.gravityApplicationMode = ContinuousMoveProviderBase.GravityApplicationMode.Immediately;
            move.leftHandMoveAction = new InputActionProperty(InputActionReference.Create(leftMove));

            var turn = Undo.AddComponent<RRXTankYawTurnProvider>(locomotionGo);
            if (turn == null)
                turn = locomotionGo.AddComponent<RRXTankYawTurnProvider>();
            if (turn == null)
                return;

            Undo.RecordObject(turn, "RRX Tank Turn");
            turn.system = locomotionSystem;
            turn.turnSpeed = 96f;
            turn.rightHandMoveAction = new InputActionProperty(InputActionReference.Create(rightMove));
        }

        static GameObject EnsureLocomotionGameObject(XROrigin xrOrigin)
        {
            var root = xrOrigin.transform;
            for (var i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == LocomotionChildName)
                    return c.gameObject;
            }

            var go = new GameObject(LocomotionChildName);
            Undo.RegisterCreatedObjectUndo(go, "RRX Locomotion");
            Undo.SetTransformParent(go.transform, root, "RRX Locomotion Parent");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
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
