using RRX.Core;
using RRX.Runtime;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace RRX.Editor
{
    /// <summary>Adds passthrough-ready camera hints to the scene XR rig.</summary>
    static class RRXMrCameraMenu
    {
        const string Prefix = "Window/RRX/";

        [MenuItem("RRX/Apply MR Camera Hints To XR Origin", false, 50)]
        [MenuItem(Prefix + "Apply MR Camera Hints To XR Origin", false, 50)]
        static void ApplyHints()
        {
            TryApplyMrCameraHints(showDialogIfNoOrigin: true);
        }

        /// <returns>True if an XR Origin was found and hints applied.</returns>
        public static bool TryApplyMrCameraHints(bool showDialogIfNoOrigin)
        {
            var origin = Object.FindObjectOfType<XROrigin>();
            if (origin == null)
            {
                if (showDialogIfNoOrigin)
                    EditorUtility.DisplayDialog("RRX MR",
                        "No XROrigin found. Add an XR Origin (XR Rig) first.", "OK");
                else
                    Debug.LogWarning("[RRX] MR camera hints skipped — no XROrigin in scene.");
                return false;
            }

            var hints = origin.GetComponent<RRXMrPresentationHints>() ??
                        Undo.AddComponent<RRXMrPresentationHints>(origin.gameObject);
            hints.ApplyNow();
            EnsurePassthroughRadiusBoundary(origin);
            EnsureArSession();
            EnsureArCameraComponents(origin);
            EditorUtility.SetDirty(origin);
            Debug.Log("[RRX] MR camera hints + AR passthrough session + depth tube applied. Player: preserve framebuffer alpha enabled. OpenXR: enable Meta Quest AR Camera (Passthrough) feature.");
            return true;
        }

        /// <summary>
        /// Scene-wide AR session so <c>XR_FB_passthrough</c> starts. One GameObject holds
        /// <see cref="ARSession"/> + <see cref="ARInputManager"/>.
        /// </summary>
        static void EnsureArSession()
        {
            var existing = Object.FindObjectOfType<ARSession>();
            if (existing != null)
                return;

            var go = new GameObject("AR Session");
            Undo.RegisterCreatedObjectUndo(go, "AR Session");
            Undo.AddComponent<ARSession>(go);
            Undo.AddComponent<ARInputManager>(go);
        }

        /// <summary>
        /// The XR camera needs <see cref="ARCameraManager"/> to drive the passthrough subsystem and
        /// <see cref="ARCameraBackground"/> to blit the real-world feed under the scene so transparent
        /// areas (the MR domain) show reality instead of black.
        /// </summary>
        static void EnsureArCameraComponents(XROrigin origin)
        {
            var cam = origin.Camera;
            if (cam == null)
                return;

            if (cam.GetComponent<ARCameraManager>() == null)
                Undo.AddComponent<ARCameraManager>(cam.gameObject);
            if (cam.GetComponent<ARCameraBackground>() == null)
                Undo.AddComponent<ARCameraBackground>(cam.gameObject);
        }

        /// <summary>
        /// Depth-only tube at <see cref="RRXPlayArea.VirtualFloorHoleRadiusMeters"/> so the inner MR domain stays
        /// real-world passthrough and outer virtual geometry depth-occludes correctly (no black paint).
        /// </summary>
        static void EnsurePassthroughRadiusBoundary(XROrigin origin)
        {
            var boundary = origin.GetComponent<RRXPassthroughRadiusBoundary>() ??
                           Undo.AddComponent<RRXPassthroughRadiusBoundary>(origin.gameObject);
            var so = new SerializedObject(boundary);
            var sync = so.FindProperty("_syncRadiusFromPlayArea");
            if (sync != null)
                sync.boolValue = true;
            var radius = so.FindProperty("_radiusMeters");
            if (radius != null)
                radius.floatValue = RRXPlayArea.VirtualFloorHoleRadiusMeters;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(boundary);
        }
    }
}
