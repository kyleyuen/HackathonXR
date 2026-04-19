using TMPro;
using RRX.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RRX.Editor
{
    static class RRXWristPanelBuilder
    {
        const string RootName = "RRX_WristObjectivePanel";

        [MenuItem("RRX/Spawn Wrist Objective Panel", false, 48)]
        [MenuItem("Window/RRX/Spawn Wrist Objective Panel", false, 48)]
        static void MenuSpawn()
        {
            SpawnOrRebuild();
            Debug.Log("[RRX] Wrist objective panel spawned.");
        }

        public static GameObject SpawnOrRebuild()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var leftController = FindLeftControllerTransform();
            if (leftController == null)
            {
                Debug.LogWarning("[RRX] Could not find left ActionBasedController for wrist panel.");
                return null;
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "RRX Wrist Panel");
            Undo.SetTransformParent(root.transform, leftController, "RRX Wrist Panel");
            root.transform.localPosition = new Vector3(0f, 0.055f, 0.015f);
            root.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            root.transform.localScale = Vector3.one * 0.0007f;

            var canvas = Undo.AddComponent<Canvas>(root);
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = Undo.AddComponent<CanvasScaler>(root);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(560f, 320f);
            Undo.AddComponent<TrackedDeviceGraphicRaycaster>(root);

            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(560f, 320f);

            var panel = CreateImage("Panel", root.transform, new Color(0.1f, 0.12f, 0.15f, 0.22f));
            Stretch(panel.rectTransform);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 12);
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var title = CreateText("Title", panel.transform, 28f, FontStyles.Bold);
            title.text = "Scenario";
            title.alignment = TextAlignmentOptions.Left;

            var hint = CreateText("Hint", panel.transform, 20f, FontStyles.Normal);
            hint.text = "Hint";
            hint.enableWordWrapping = true;

            var meta = CreateText("Meta", panel.transform, 16f, FontStyles.Italic);
            meta.text = "Mistakes: 0/3";

            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(row, "RRX Wrist Panel Buttons");
            row.transform.SetParent(panel.transform, false);
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            var hintButton = CreateButton("Hint", row.transform);
            var resetButton = CreateButton("Reset", row.transform);
            resetButton.gameObject.SetActive(false);

            var panelScript = Undo.AddComponent<RRXWristObjectivePanel>(root);
            panelScript.Configure(title, hint, meta, hintButton, resetButton, panel);
            return root;
        }

        static Transform FindLeftControllerTransform()
        {
            foreach (var controller in Object.FindObjectsOfType<ActionBasedController>(true))
            {
                if (controller.name.ToLowerInvariant().Contains("left"))
                    return controller.transform;
            }

            return null;
        }

        static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        static TMP_Text CreateText(string name, Transform parent, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            return text;
        }

        static Button CreateButton(string label, Transform parent)
        {
            var buttonGo = new GameObject($"{label}_Button",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(buttonGo, label);
            buttonGo.transform.SetParent(parent, false);
            buttonGo.GetComponent<Image>().color = new Color(0.23f, 0.29f, 0.35f, 0.9f);
            var le = buttonGo.GetComponent<LayoutElement>();
            le.minHeight = 42f;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(textGo, $"{label} Text");
            textGo.transform.SetParent(buttonGo.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());
            var txt = textGo.GetComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 22f;
            txt.color = Color.white;

            return buttonGo.GetComponent<Button>();
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
