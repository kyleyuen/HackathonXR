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
            EditorUtility.SetDirty(origin);
            Debug.Log("[RRX] MR camera hints applied (SolidColor, alpha 0 background). After importing com.unity.xr.meta-openxr: Project Settings > XR Plug-in Management > OpenXR > Android > Meta Quest feature group > enable Camera / passthrough-related features per Unity OpenXR Meta docs.");
            return true;
        }
    }
}
