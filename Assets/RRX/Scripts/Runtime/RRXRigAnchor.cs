using UnityEngine;

namespace RRX.Runtime
{
    /// <summary>
    /// Pins the XR rig root to its starting world pose so room-scale tracking never "drifts" the origin.
    /// Head/hand tracking still applies to child camera/controllers; only the rig transform is clamped.
    /// </summary>
    /// <remarks>
    /// The rig is held at its initial XZ + yaw. Y uses the initial Y so floor-tracking height changes
    /// do not slide the rig vertically.
    /// </remarks>
    [DefaultExecutionOrder(5000)]
    [DisallowMultipleComponent]
    public sealed class RRXRigAnchor : MonoBehaviour
    {
        [SerializeField] bool _lockPosition = true;
        [SerializeField] bool _lockYaw = true;

        Vector3 _anchorPosition;
        Quaternion _anchorRotation;

        void OnEnable()
        {
            CaptureAnchor();
        }

        void CaptureAnchor()
        {
            _anchorPosition = transform.position;
            _anchorRotation = transform.rotation;
        }

        void LateUpdate()
        {
            if (_lockPosition && transform.position != _anchorPosition)
                transform.position = _anchorPosition;

            if (_lockYaw && transform.rotation != _anchorRotation)
                transform.rotation = _anchorRotation;
        }
    }
}
