using UnityEngine;

namespace RRX.Runtime
{
    /// <summary>
    /// MR-friendly camera presentation: transparent clear so passthrough / composition can show through.
    /// Player Settings should have <b>preserve framebuffer alpha</b> enabled on Android for Quest composition.
    /// Attach on <see cref="Unity.XR.CoreUtils.XROrigin"/> root or run <c>RRX / Apply MR Camera Hints</c> from the editor menu.
    /// Requires Meta Quest Camera / passthrough features enabled under XR Plug-in Management (Unity OpenXR: Meta).
    /// </summary>
    public sealed class RRXMrPresentationHints : MonoBehaviour
    {
        [SerializeField] bool _applyOnEnable = true;

        void OnEnable()
        {
            if (_applyOnEnable)
                ApplyNow();
        }

        [ContextMenu("Apply MR camera hints now")]
        public void ApplyNow()
        {
            foreach (var cam in GetComponentsInChildren<Camera>(true))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
        }
    }
}
