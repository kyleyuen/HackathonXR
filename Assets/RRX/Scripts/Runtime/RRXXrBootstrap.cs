using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RRX.Runtime
{
    /// <summary>
    /// Runs before child controller/rig Awake so EventSystem + <see cref="XRUIInputModule"/> exist
    /// when ray interactors enable. Aggressively strips locomotion providers, anchors the rig,
    /// enables input actions, and forces ray interactor UI mode so controllers can click world canvases.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    [DisallowMultipleComponent]
    public sealed class RRXXrBootstrap : MonoBehaviour
    {
        void Awake()
        {
            StripLocomotionAndPhysics();
            RRXRigInteractionSetup.ConfigureSceneEventSystems();
            EnsureRigAnchor();
            EnsureWorldAnchorService();
        }

        IEnumerator Start()
        {
            EnableAllInputActions();
            ForceRayInteractorsXrUi();
            yield return null;
            // Some XRI internal state initializes one frame late; retoggle so interactors re-register
            // with XRUIInputModule and light up their line visuals.
            ForceRayInteractorsXrUi();
        }

        void StripLocomotionAndPhysics()
        {
            foreach (var lp in GetComponentsInChildren<LocomotionProvider>(true))
                Destroy(lp);

            foreach (var ls in GetComponentsInChildren<LocomotionSystem>(true))
                Destroy(ls);

            var cc = GetComponent<CharacterController>();
            if (cc != null)
                Destroy(cc);

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
                Destroy(rb);
        }

        void EnsureRigAnchor()
        {
            if (GetComponent<RRXRigAnchor>() == null)
                gameObject.AddComponent<RRXRigAnchor>();
        }

        static void EnsureWorldAnchorService()
        {
            if (FindObjectOfType<RRXWorldAnchorService>() != null)
                return;

            var host = new GameObject("RRX_WorldAnchor");
            host.AddComponent<RRXWorldAnchorService>();
        }

        void EnableAllInputActions()
        {
            foreach (var mgr in GetComponents<InputActionManager>())
                mgr.EnableInput();
        }

        void ForceRayInteractorsXrUi()
        {
            foreach (var ray in GetComponentsInChildren<XRRayInteractor>(true))
            {
                ray.enableUIInteraction = true;
                ray.enabled = false;
                ray.enabled = true;

                var viz = ray.GetComponent<XRInteractorLineVisual>();
                if (viz != null)
                {
                    viz.enabled = true;
                    viz.lineLength = Mathf.Max(viz.lineLength, 5f);
                }

                var lr = ray.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.enabled = true;
                    lr.sortingOrder = 320;
                    if (lr.startWidth < 0.005f) lr.startWidth = 0.008f;
                    if (lr.endWidth < 0.002f) lr.endWidth = 0.003f;
                    EnsureLineMaterial(lr);
                }
            }
        }

        /// <summary>
        /// Stock <see cref="LineRenderer"/> with a missing material renders magenta or invisible on some
        /// devices; fall back to the engine default so lasers always show.
        /// </summary>
        static void EnsureLineMaterial(LineRenderer lr)
        {
            if (lr.sharedMaterial != null)
                return;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader != null)
                lr.material = new Material(shader);
        }
    }
}
