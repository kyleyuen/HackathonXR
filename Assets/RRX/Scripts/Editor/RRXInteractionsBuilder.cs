using System.Collections.Generic;
using RRX.Core;
using RRX.Interactions;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RRX.Editor
{
    static class RRXInteractionsBuilder
    {
        [MenuItem("RRX/Bind Trigger Hotspots", false, 46)]
        [MenuItem("Window/RRX/Bind Trigger Hotspots", false, 46)]
        static void MenuBind()
        {
            BindTriggerHotspots();
            Debug.Log("[RRX] Trigger hotspots rebound to L/R UI Press actions.");
        }

        public static void BindTriggerHotspots()
        {
            var refsByName = LoadInputActionReferencesByName(RRXDemoSceneWizard.InputActionsAssetPath);
            if (refsByName == null || refsByName.Count == 0)
                return;

            refsByName.TryGetValue("XRI LeftHand Interaction/UI Press", out var left);
            refsByName.TryGetValue("XRI RightHand Interaction/UI Press", out var right);

            var runner = Object.FindObjectOfType<ScenarioRunner>();
            foreach (var hotspot in Object.FindObjectsOfType<RRXTriggerActivatedHotspot>(true))
            {
                if (runner != null)
                    hotspot.SetRunner(runner);
                hotspot.SetUiPressActions(left, right);
                var tag = hotspot.GetComponent<RRXScenarioHotspotTag>();
                if (tag != null)
                    hotspot.SetHotspotTag(tag);
                EditorUtility.SetDirty(hotspot);
            }
        }

        static Dictionary<string, InputActionReference> LoadInputActionReferencesByName(string path)
        {
            var map = new Dictionary<string, InputActionReference>();
            var subs = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var o in subs)
            {
                if (o is InputActionReference r && r.action != null)
                {
                    var key = $"{r.action.actionMap?.name}/{r.action.name}";
                    map[key] = r;
                }
            }

            return map;
        }
    }
}
