using System.Linq;
using RRX.Core;
using RRX.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RRX.Editor
{
    /// <summary>
    /// Idempotent world-space MR UI + XR UI EventSystem for <see cref="RRXDemoSceneWizard"/>.
    /// </summary>
    static class RRXWorldUiSetup
    {
        public const string UiRootName = "RRX_UI_Root";
        const string CanvasName = "RRX_WorldCanvas";
        const string EventSystemName = "RRX_EventSystem";
        const string TextNameStep = "RRX_Text_Step";
        const string TextNameHint = "RRX_Text_Hint";

        const float CanvasScale = 0.002f;
        static readonly Vector2 CanvasSize = new Vector2(920f, 560f);

        internal static void EnsureWorldTrainingUi(GameObject scenario, ScenarioRunner runner)
        {
            if (scenario == null || runner == null)
                return;

            EnsureXrEventSystem();
            EnsureUiHierarchy(scenario, runner);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void EnsureXrEventSystem()
        {
            var systems = Object.FindObjectsOfType<EventSystem>();
            EventSystem primary = null;

            foreach (var es in systems)
            {
                if (es.gameObject.name == EventSystemName)
                {
                    primary = es;
                    break;
                }
            }

            primary = primary ?? (systems.Length > 0 ? systems[0] : null);

            if (primary == null)
            {
                var go = new GameObject(EventSystemName);
                Undo.RegisterCreatedObjectUndo(go, "RRX EventSystem");
                primary = Undo.AddComponent<EventSystem>(go);
            }
            else if (primary.gameObject.name != EventSystemName)
            {
                Undo.RecordObject(primary.gameObject, "RRX EventSystem rename");
                primary.gameObject.name = EventSystemName;
            }

            StripOtherInputModules(primary.gameObject);
            EnsureXrUiInputModule(primary.gameObject);

            var survivors = Object.FindObjectsOfType<EventSystem>();
            foreach (var es in survivors)
            {
                if (es != null && es != primary)
                    Undo.DestroyObjectImmediate(es.gameObject);
            }
        }

        static void StripOtherInputModules(GameObject eventGo)
        {
            foreach (var sm in eventGo.GetComponents<StandaloneInputModule>())
                Undo.DestroyObjectImmediate(sm);
            foreach (var im in eventGo.GetComponents<InputSystemUIInputModule>())
                Undo.DestroyObjectImmediate(im);
        }

        static void EnsureXrUiInputModule(GameObject eventGo)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(RRXLocomotionSetup.InputActionsAssetPath);
            if (asset == null)
            {
                Debug.LogError(
                    $"[RRX] World UI skipped Input actions: missing '{RRXLocomotionSetup.InputActionsAssetPath}'.");
                return;
            }

            var map = asset.FindActionMap("XRI UI");
            if (map == null)
            {
                Debug.LogError("[RRX] Input actions missing 'XRI UI' map; cannot configure XRUIInputModule.");
                return;
            }

            var xui = eventGo.GetComponent<XRUIInputModule>();
            if (xui == null)
                xui = Undo.AddComponent<XRUIInputModule>(eventGo);

            Undo.RecordObject(xui, "RRX XR UI Input Module");
            xui.activeInputMode = XRUIInputModule.ActiveInputMode.InputSystemActions;
            xui.enableXRInput = true;
            xui.enableMouseInput = true;
            xui.enableTouchInput = false;

            bool Bind(string actionName, System.Action<InputAction> apply)
            {
                var action = map.FindAction(actionName);
                if (action == null)
                {
                    Debug.LogError($"[RRX] Input actions missing '{actionName}' under XRI UI.");
                    return false;
                }

                apply(action);
                return true;
            }

            if (!Bind("Point", a => xui.pointAction = InputActionReference.Create(a))) return;
            if (!Bind("Click", a => xui.leftClickAction = InputActionReference.Create(a))) return;
            if (!Bind("MiddleClick", a => xui.middleClickAction = InputActionReference.Create(a))) return;
            if (!Bind("RightClick", a => xui.rightClickAction = InputActionReference.Create(a))) return;
            if (!Bind("ScrollWheel", a => xui.scrollWheelAction = InputActionReference.Create(a))) return;
            if (!Bind("Navigate", a => xui.navigateAction = InputActionReference.Create(a))) return;
            if (!Bind("Submit", a => xui.submitAction = InputActionReference.Create(a))) return;
            if (!Bind("Cancel", a => xui.cancelAction = InputActionReference.Create(a))) return;
        }

        static void EnsureUiHierarchy(GameObject scenario, ScenarioRunner runner)
        {
            var playR = RRXPlayArea.RadiusMeters;
            RemoveDuplicateUiRoots(scenario.transform);

            var uiRoot = scenario.transform.Find(UiRootName)?.gameObject;
            if (uiRoot == null)
            {
                uiRoot = new GameObject(UiRootName);
                Undo.RegisterCreatedObjectUndo(uiRoot, "RRX UI Root");
                Undo.SetTransformParent(uiRoot.transform, scenario.transform, "RRX UI Root Parent");
            }

            uiRoot.transform.localPosition = new Vector3(0f, 1.55f, playR * 0.26f);
            uiRoot.transform.localRotation = Quaternion.identity;
            uiRoot.transform.localScale = Vector3.one;

            var binder = uiRoot.GetComponent<RRXTrainingHudController>() ??
                         Undo.AddComponent<RRXTrainingHudController>(uiRoot);
            BindRunner(binder, runner);

            var canvasTf = uiRoot.transform.Find(CanvasName);
            GameObject canvasGo;
            if (canvasTf == null)
            {
                canvasGo = CreateCanvas(uiRoot.transform);
                Undo.RegisterCreatedObjectUndo(canvasGo, "RRX World Canvas");
            }
            else
            {
                canvasGo = canvasTf.gameObject;
                ConfigureCanvas(canvasGo);
            }

            EnsureTrackedRaycaster(canvasGo);
            BuildOrRefreshPanel(canvasGo.transform);
        }

        static void RemoveDuplicateUiRoots(Transform scenario)
        {
            var roots = Enumerable.Range(0, scenario.childCount)
                .Select(i => scenario.GetChild(i))
                .Where(t => t.name == UiRootName)
                .ToArray();
            for (var i = 1; i < roots.Length; i++)
                Undo.DestroyObjectImmediate(roots[i].gameObject);
        }

        static void BindRunner(RRXTrainingHudController binder, ScenarioRunner runner)
        {
            var so = new SerializedObject(binder);
            var p = so.FindProperty("_runner");
            if (p != null)
            {
                p.objectReferenceValue = runner;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static GameObject CreateCanvas(Transform parent)
        {
            var go = new GameObject(CanvasName);
            Undo.SetTransformParent(go.transform, parent, "RRX Canvas Parent");
            ConfigureCanvas(go);
            return go;
        }

        static void ConfigureCanvas(GameObject canvasGo)
        {
            Undo.RecordObject(canvasGo.transform, "RRX Canvas Transform");
            canvasGo.transform.localPosition = Vector3.zero;
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = new Vector3(CanvasScale, CanvasScale, CanvasScale);

            var canvas = canvasGo.GetComponent<Canvas>() ?? Undo.AddComponent<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = null;

            var scaler = canvasGo.GetComponent<CanvasScaler>() ?? Undo.AddComponent<CanvasScaler>(canvasGo);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = CanvasSize;

            Undo.RecordObject(canvas, "RRX Canvas props");
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 10;
        }

        static void EnsureTrackedRaycaster(GameObject canvasGo)
        {
            foreach (var gr in canvasGo.GetComponents<GraphicRaycaster>())
                Undo.DestroyObjectImmediate(gr);

            if (canvasGo.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                Undo.AddComponent<TrackedDeviceGraphicRaycaster>(canvasGo);
        }

        static void BuildOrRefreshPanel(Transform canvasTf)
        {
            const string panelName = "RRX_Panel";

            var existing = canvasTf.Find(panelName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            var panel = new GameObject(panelName);
            Undo.RegisterCreatedObjectUndo(panel, "RRX UI Panel");
            Undo.SetTransformParent(panel.transform, canvasTf, "RRX Panel Parent");

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.09f, 0.14f, 0.94f);
            bg.raycastTarget = true;

            var rect = panel.GetComponent<RectTransform>();
            StretchFull(rect);

            CreateHeader(panel.transform);
            CreateRow(panel.transform);
        }

        static void CreateHeader(Transform panel)
        {
            var header = new GameObject("RRX_Header");
            Undo.RegisterCreatedObjectUndo(header, "RRX Header");
            Undo.SetTransformParent(header.transform, panel, "RRX Header Parent");
            var hRect = header.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 0.78f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.offsetMin = new Vector2(16f, 0f);
            hRect.offsetMax = new Vector2(-16f, -12f);

            AddTmp(header.transform, "RRX_Title", new Vector2(0f, 0.72f), new Vector2(1f, 1f),
                "Rewind Rescue XR", 40, FontStyles.Bold);
            AddTmp(header.transform, TextNameStep, new Vector2(0f, 0.38f), new Vector2(1f, 0.7f),
                "Loading…", 26, FontStyles.Normal);
            AddTmp(header.transform, TextNameHint, new Vector2(0f, 0f), new Vector2(1f, 0.36f),
                string.Empty, 22, FontStyles.Italic);
        }

        static void CreateRow(Transform panel)
        {
            var row = new GameObject("RRX_ButtonRow");
            Undo.RegisterCreatedObjectUndo(row, "RRX Button Row");
            Undo.SetTransformParent(row.transform, panel, "RRX Row Parent");
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.04f, 0.04f);
            rowRect.anchorMax = new Vector2(0.96f, 0.72f);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(8, 8, 8, 8);

            CreateActionButton(row.transform, "RRX_Btn_CheckResponsiveness", "1 — Check", new Color(0.2f, 0.45f,
                0.85f));
            CreateActionButton(row.transform, "RRX_Btn_Call911", "2 — 911", new Color(0.25f, 0.55f, 0.35f));
            CreateActionButton(row.transform, "RRX_Btn_Narcan", "3 — Narcan", new Color(0.65f, 0.35f, 0.2f));
            CreateActionButton(row.transform, "RRX_Btn_Rewind", "Rewind", new Color(0.35f, 0.35f, 0.42f));
        }

        static void CreateActionButton(Transform parent, string objectName, string label, Color baseColor)
        {
            var go = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(go, objectName);
            Undo.SetTransformParent(go.transform, parent, "RRX Btn Parent");

            var img = go.AddComponent<Image>();
            img.color = baseColor;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = baseColor * 1.15f;
            colors.pressedColor = baseColor * 0.85f;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.38f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 96f;
            le.preferredHeight = 112f;
            le.flexibleWidth = 1f;

            var labelGo = new GameObject("Label");
            Undo.RegisterCreatedObjectUndo(labelGo, "RRX Btn Label");
            Undo.SetTransformParent(labelGo.transform, go.transform, "RRX Btn Label Parent");
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 28f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            ApplyDefaultTmpFont(tmp);

            var lr = labelGo.GetComponent<RectTransform>();
            StretchFull(lr);
        }

        static void AddTmp(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, string text,
            float fontSize, FontStyles style)
        {
            var go = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(go, objectName);
            Undo.SetTransformParent(go.transform, parent, "RRX TMP Parent");
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            ApplyDefaultTmpFont(tmp);
        }

        static void ApplyDefaultTmpFont(TextMeshProUGUI tmp)
        {
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
