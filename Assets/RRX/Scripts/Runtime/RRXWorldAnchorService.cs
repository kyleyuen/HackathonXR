using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace RRX.Runtime
{
    /// <summary>
    /// Re-anchors the entire virtual scene (mall blockout, patient, ambience, feedback, etc.) to the
    /// user's current XR-camera pose so the experience always builds around wherever the player is
    /// physically standing and facing. Safe to run at startup or on-demand — e.g. when the player moves
    /// to a new room or re-centers via a Settings button.
    /// </summary>
    [DefaultExecutionOrder(-1900)]
    [DisallowMultipleComponent]
    public sealed class RRXWorldAnchorService : MonoBehaviour
    {
        /// <summary>Names of root transforms in the scene that represent "world / scenario content".</summary>
        static readonly string[] WorldRootNames =
        {
            "RRX_Environment_Root",
            "RRX_Patient",
            "RRX_Feedback",
            "RRX_Ambience",
            "RRX_PlayArea",
            "RRX_MR_Domain",
            "RRX_Passthrough",
            "RRX_UI_Root"
        };

        static RRXWorldAnchorService _instance;

        [SerializeField] bool _anchorOnStart = true;
        [SerializeField] float _initialDelayFrames = 2;
        [SerializeField] bool _alignYawToCamera = true;

        bool _hasAnchored;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
        }

        IEnumerator Start()
        {
            if (!_anchorOnStart)
                yield break;

            // Tracking origin needs at least one frame to report a real headset pose, otherwise
            // we'd re-anchor to a stale (0, 0, 0) and push the scene into the ground.
            for (int i = 0; i < Mathf.Max(1, _initialDelayFrames); i++)
                yield return null;

            AnchorWorldToCurrentHead();
        }

        /// <summary>Convenience: call from UI buttons / other services without a direct reference.</summary>
        public static void AnchorNow()
        {
            if (_instance != null)
                _instance.AnchorWorldToCurrentHead();
            else
                AnchorWorldStatic(alignYawToCamera: true);
        }

        public void AnchorWorldToCurrentHead()
        {
            AnchorWorldStatic(_alignYawToCamera);
            _hasAnchored = true;
        }

        /// <summary>
        /// Shifts every known world root so their previous world-space pose is expressed relative to the
        /// XR rig's current head pose. Yaw-only rotation (we never tilt the floor).
        /// </summary>
        static void AnchorWorldStatic(bool alignYawToCamera)
        {
            var origin = FindObjectOfType<XROrigin>();
            if (origin == null)
            {
                Debug.LogWarning("[RRX] RRXWorldAnchorService: no XROrigin in scene; skipping re-anchor.");
                return;
            }

            var cam = origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return;

            // Target origin for the world = XR rig root position, flattened to floor Y.
            Vector3 originPos = origin.transform.position;
            Vector3 headPos   = cam.position;

            // Project the camera's forward onto the floor plane to get a clean yaw.
            Vector3 headForward = cam.forward;
            headForward.y = 0f;
            if (headForward.sqrMagnitude < 0.0001f)
                headForward = origin.transform.forward;
            headForward.Normalize();

            // Conceptually we want the virtual scene's "origin point" (where it was originally built at
            // world (0,0,0) facing +Z) to coincide with the rig root, facing where the user is looking.
            // Build a delta transform that maps old world (0,0,0, identity) → new target pose.
            Quaternion targetYaw = alignYawToCamera
                ? Quaternion.LookRotation(headForward, Vector3.up)
                : Quaternion.identity;

            // Preserve the rig's floor height (origin Y) — don't move the floor up/down with head height.
            Vector3 targetPos = new Vector3(originPos.x, originPos.y, originPos.z);

            ApplyDeltaToWorldRoots(targetPos, targetYaw);

            // After relocating the mall, force RRXRigAnchor to re-capture so its clamp pose matches
            // the new rig-relative baseline (it only captures once on enable otherwise).
            foreach (var anchor in FindObjectsOfType<RRXRigAnchor>())
                anchor.ReCapture();
        }

        /// <summary>
        /// Computes the new pose each world root should have and applies it, preserving their pose
        /// <em>relative to world-origin</em>. Equivalent to parenting them all under a pivot at (0,0,0)
        /// and moving that pivot to (targetPos, targetYaw).
        /// </summary>
        static void ApplyDeltaToWorldRoots(Vector3 targetPos, Quaternion targetYaw)
        {
            var roots = CollectWorldRoots();
            foreach (var root in roots)
            {
                if (root == null) continue;

                // New position = targetPos + targetYaw * oldWorldPos
                Vector3 newPos     = targetPos + targetYaw * root.position;
                Quaternion newRot  = targetYaw * root.rotation;
                root.SetPositionAndRotation(newPos, newRot);
            }
        }

        static List<Transform> CollectWorldRoots()
        {
            var list = new List<Transform>(WorldRootNames.Length);
            foreach (var name in WorldRootNames)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;

                // Skip if this object is parented under the XR rig (would cause double-transform).
                var oxr = FindObjectOfType<XROrigin>();
                if (oxr != null && go.transform.IsChildOf(oxr.transform))
                    continue;

                list.Add(go.transform);
            }
            return list;
        }

        public bool HasAnchored => _hasAnchored;
    }
}
