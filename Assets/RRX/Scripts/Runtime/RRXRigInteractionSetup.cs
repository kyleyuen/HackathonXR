using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RRX.Runtime
{
    /// <summary>
    /// Central place for XR UI / EventSystem wiring. Must run early so <see cref="UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor"/>
    /// registers with <see cref="XRUIInputModule"/> before input is processed.
    /// </summary>
    public static class RRXRigInteractionSetup
    {
        /// <summary>
        /// Single EventSystem with only <see cref="XRUIInputModule"/> (no competing InputSystemUIInputModule).
        /// </summary>
        public static void ConfigureSceneEventSystems()
        {
            ConsolidateToSingleEventSystem();

            var es = Object.FindObjectOfType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("RRX_EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            StripNonXrInputModules(es.gameObject);

            var xr = es.GetComponent<XRUIInputModule>();
            if (xr == null)
                xr = es.gameObject.AddComponent<XRUIInputModule>();

            xr.activeInputMode = XRUIInputModule.ActiveInputMode.InputSystemActions;
            xr.enableXRInput = true;
        }

        static void ConsolidateToSingleEventSystem()
        {
            var all = Object.FindObjectsOfType<EventSystem>();
            if (all.Length <= 1)
                return;

            EventSystem keeper = null;
            foreach (var es in all)
            {
                if (es.gameObject.name.IndexOf("RRX_EventSystem", System.StringComparison.Ordinal) >= 0)
                {
                    keeper = es;
                    break;
                }
            }

            keeper ??= all[0];

            foreach (var es in all)
            {
                if (es != keeper)
                    Object.Destroy(es.gameObject);
            }
        }

        static void StripNonXrInputModules(GameObject eventSystemGo)
        {
            foreach (var m in eventSystemGo.GetComponents<StandaloneInputModule>())
                Object.Destroy(m);

#if ENABLE_INPUT_SYSTEM
            foreach (var m in eventSystemGo.GetComponents<UnityEngine.InputSystem.UI.InputSystemUIInputModule>())
                Object.Destroy(m);
#endif
        }
    }
}
