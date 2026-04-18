using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace RRX.Editor
{
    /// <summary>
    /// XR smooth move + snap turn + CharacterController collision. Integrates XRI Starter input actions when present.
    /// Runs from <see cref="RRXDemoSceneWizard"/> full MR pipeline so Quest builds stay one-click.
    /// </summary>
    static class RRXLocomotionSetup
    {
        const string LocomotionChildName = "RRX_Locomotion";

        internal const string InputActionsAssetPath =
            "Assets/RRX/Input/XRI Default Input Actions.inputactions";

        /// <summary>Called by <see cref="RRXDemoSceneWizard"/> after XR Origin exists.</summary>
        public static void EnsureLocomotionAndCollision()
        {
            var xrOrigin = Object.FindObjectOfType<XROrigin>();
            if (xrOrigin == null)
            {
                Debug.LogWarning("[RRX] Locomotion setup skipped — no XR Origin in scene.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath);
            if (asset == null)
            {
                Debug.LogError(
                    $"[RRX] Missing '{InputActionsAssetPath}'. Restore from XR Interaction Toolkit Starter Assets or reinstall packages.");
                return;
            }

            BindInputActionManager(xrOrigin.gameObject, asset);
            EnsureCharacterController(xrOrigin);
            EnsureLocomotionComponents(xrOrigin, asset);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("RRX/Apply XR Locomotion + Collision", false, 35)]
        [MenuItem("Window/RRX/Apply XR Locomotion + Collision", false, 35)]
        static void MenuApplyLocomotion()
        {
            EnsureLocomotionAndCollision();
            Debug.Log("[RRX] XR locomotion + collision applied. Save the scene.");
        }

        static void BindInputActionManager(GameObject xrOriginRoot, InputActionAsset asset)
        {
            var mam = xrOriginRoot.GetComponent<InputActionManager>() ??
                      Undo.AddComponent<InputActionManager>(xrOriginRoot);

            var so = new SerializedObject(mam);
            var prop = so.FindProperty("m_ActionAssets");
            if (prop == null || !prop.isArray)
                return;

            for (var i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == asset)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }

            prop.arraySize++;
            prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = asset;
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
            {
                Debug.LogError(
                    "[RRX] Could not add CharacterController on XR Origin rig root. Check for conflicting components.");
                return;
            }

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

            var sysSo = new SerializedObject(locomotionSystem);
            var originProp = sysSo.FindProperty("m_XROrigin");
            if (originProp != null)
                originProp.objectReferenceValue = xrOrigin;
            sysSo.ApplyModifiedPropertiesWithoutUndo();

            var leftMove = asset.FindAction("XRI LeftHand Locomotion/Move");
            var rightMove = asset.FindAction("XRI RightHand Locomotion/Move");
            var leftSnap = asset.FindAction("XRI LeftHand Locomotion/Snap Turn");
            var rightSnap = asset.FindAction("XRI RightHand Locomotion/Snap Turn");

            if (leftMove == null || rightMove == null || leftSnap == null || rightSnap == null)
            {
                Debug.LogError("[RRX] Locomotion actions missing in Input Action Asset. Expected XRI Starter action names.");
                return;
            }

            var move = locomotionGo.GetComponent<ActionBasedContinuousMoveProvider>() ??
                       Undo.AddComponent<ActionBasedContinuousMoveProvider>(locomotionGo);

            Undo.RecordObject(move, "RRX Continuous Move");
            move.system = locomotionSystem;
            move.moveSpeed = 2f;
            move.enableStrafe = true;
            move.enableFly = false;
            move.useGravity = true;
            move.gravityApplicationMode = ContinuousMoveProviderBase.GravityApplicationMode.Immediately;
            move.leftHandMoveAction = new InputActionProperty(leftMove);
            move.rightHandMoveAction = new InputActionProperty(rightMove);

            var snap = locomotionGo.GetComponent<ActionBasedSnapTurnProvider>() ??
                       Undo.AddComponent<ActionBasedSnapTurnProvider>(locomotionGo);

            Undo.RecordObject(snap, "RRX Snap Turn");
            snap.system = locomotionSystem;
            snap.turnAmount = 45f;
            snap.debounceTime = 0.35f;
            snap.enableTurnLeftRight = true;
            snap.enableTurnAround = false;
            snap.delayTime = 0f;
            snap.leftHandSnapTurnAction = new InputActionProperty(leftSnap);
            snap.rightHandSnapTurnAction = new InputActionProperty(rightSnap);
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
    }
}
