using RRX.Core;
using RRX.Runtime;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;

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
            EditorUtility.SetDirty(origin);
            Debug.Log("[RRX] MR camera hints + passthrough depth tube applied (transparent clear; hole matches RRXPlayArea virtual floor). Player: preserve framebuffer alpha enabled. Meta OpenXR Android: enable Camera / passthrough features.");
            return true;
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
