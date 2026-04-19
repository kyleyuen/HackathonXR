using System.Collections;
using UnityEngine;

namespace RRX.Runtime
{
    /// <summary>
    /// Pins the XR rig <em>root</em> to its starting world pose so room-scale tracking never "drifts"
    /// the origin. Head/hand tracking still applies to the child camera/controllers — only the rig root
    /// transform is clamped — so physical walking inside the room translates the camera normally through
    /// the virtual scene without the rig itself sliding.
    /// </summary>
    /// <remarks>
    /// The starting pose is re-captured on the first two tracked frames so we never lock onto a stale
    /// (0, 0, 0) pose captured before the XR subsystem reported the initial floor-tracked position.
    /// </remarks>
    [DefaultExecutionOrder(5000)]
    [DisallowMultipleComponent]
    public sealed class RRXRigAnchor : MonoBehaviour
    {
        [SerializeField] bool _lockPosition = true;
        [SerializeField] bool _lockYaw = true;

        Vector3 _anchorPosition;
        Quaternion _anchorRotation;
        bool _anchorReady;

        void OnEnable()
        {
            CaptureAnchor();
            StartCoroutine(RecaptureAfterTrackingSettles());
        }

        IEnumerator RecaptureAfterTrackingSettles()
        {
            yield return null;
            CaptureAnchor();
            yield return null;
            CaptureAnchor();
            _anchorReady = true;
        }

        void CaptureAnchor()
        {
            _anchorPosition = transform.position;
            _anchorRotation = transform.rotation;
        }

        /// <summary>
        /// Force the anchor to re-sample the current rig pose. Call this after the world has been
        /// relocated (e.g. by <see cref="RRXWorldAnchorService"/>) so the clamp target matches the
        /// new rig-relative baseline instead of snapping back to the pre-move pose.
        /// </summary>
        public void ReCapture()
        {
            CaptureAnchor();
            _anchorReady = true;
        }

        void LateUpdate()
        {
            if (!_anchorReady)
                return;

            if (_lockPosition && transform.position != _anchorPosition)
                transform.position = _anchorPosition;

            if (_lockYaw && transform.rotation != _anchorRotation)
                transform.rotation = _anchorRotation;
        }
    }
}
