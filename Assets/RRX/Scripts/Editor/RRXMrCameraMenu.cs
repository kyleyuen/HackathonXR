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
            StripPassthroughRadiusBoundary(origin);
            EnsureArSession();
            EnsureArCameraComponents(origin);
            EditorUtility.SetDirty(origin);
            Debug.Log("[RRX] MR camera hints + AR passthrough session applied. Virtual walls visible; MR bleed comes from translucent plaza tiles. Player: preserve framebuffer alpha enabled. OpenXR: enable Meta Quest AR Camera (Passthrough) feature.");
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
        /// Removes the legacy head-following depth tube. The tube was depth-occluding all virtual geometry
        /// beyond its radius (including the mall walls), turning the horizon into solid passthrough. The MR
        /// bleed now comes from translucent center tiles on the plaza floor, so the tube is no longer needed.
        /// </summary>
        static void StripPassthroughRadiusBoundary(XROrigin origin)
        {
            var boundary = origin.GetComponent<RRXPassthroughRadiusBoundary>();
            if (boundary != null)
                Undo.DestroyObjectImmediate(boundary);

            for (var i = origin.transform.childCount - 1; i >= 0; i--)
            {
                var child = origin.transform.GetChild(i);
                if (child != null && child.name == "RRX_PassthroughOccluderTube")
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }
}
