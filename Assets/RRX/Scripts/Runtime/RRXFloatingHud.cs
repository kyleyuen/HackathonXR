using TMPro;
using RRX.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RRX.UI
{
    /// <summary>
    /// World-space HUD parented to the XR headset camera: start menu (Start / Settings / Exit / Help),
    /// then splits into left (menus) and right (tools + inventory). Back returns to the start menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RRXFloatingHud : MonoBehaviour
    {
        const float PanelOpacity = 0.2f;
        const float ButtonBackdropAlpha = 0.85f;
        const int CanvasRefPixels = 900;
        const int CanvasRefPixelsY = 560;

        [SerializeField] float _forwardMeters = 0.72f;
        [SerializeField] float _downMeters = 0.05f;

        bool _hudBuilt;
        bool _canvasCameraAssigned;
        Canvas _canvas;
        CanvasScaler _scaler;
        RectTransform _rootRect;
        GameObject _mainMenuPanel;
        GameObject _splitPanel;
        GameObject _leftColumn;
        GameObject _rightColumn;

        void Awake()
        {
            if (_hudBuilt)
                return;

            EnsureEventSystem();
            BuildHud();
            _hudBuilt = true;
        }

        void Reset()
        {
            if (transform.parent == null)
                TryParentToXrCamera();
        }

        void OnEnable()
        {
            TryParentToXrCamera();
        }

        void LateUpdate()
        {
            if (_canvasCameraAssigned || _canvas == null)
                return;

            var oxr = FindObjectOfType<XROrigin>();
            if (oxr != null && oxr.Camera != null)
            {
                _canvas.worldCamera = oxr.Camera;
                _canvasCameraAssigned = true;
            }
        }

        void TryParentToXrCamera()
        {
            var origin = FindObjectOfType<XROrigin>();
            var cam = origin != null ? origin.Camera : Camera.main;
            if (cam == null)
                return;

            if (transform.parent == cam.transform)
                return;

            transform.SetParent(cam.transform, false);
            transform.localPosition = new Vector3(0f, -_downMeters, _forwardMeters);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// XR world UI requires <see cref="XRUIInputModule"/> — not plain <see cref="UnityEngine.InputSystem.UI.InputSystemUIInputModule"/>.
        /// Otherwise controller rays (<see cref="UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor"/>) never click UI.
        /// </summary>
        static void EnsureEventSystem()
        {
            RRXRigInteractionSetup.ConfigureSceneEventSystems();
        }

        void BuildHud()
        {
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null)
                _canvas = gameObject.AddComponent<Canvas>();

            _canvas.renderMode = RenderMode.WorldSpace;
            var oxr = FindObjectOfType<XROrigin>();
            _canvas.worldCamera = oxr != null ? oxr.Camera : null;
            _canvasCameraAssigned = _canvas.worldCamera != null;

            _scaler = gameObject.GetComponent<CanvasScaler>();
            if (_scaler == null)
                _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(CanvasRefPixels, CanvasRefPixelsY);
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = gameObject.GetComponent<RectTransform>();
            if (rt == null)
                rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CanvasRefPixels, CanvasRefPixelsY);
            rt.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            _rootRect = rt;

            var backdrop = CreateUiObject<Image>("Backdrop", rt);
            StretchFull(backdrop.rectTransform);
            backdrop.color = new Color(0.06f, 0.07f, 0.09f, PanelOpacity);
            backdrop.raycastTarget = true;

            _mainMenuPanel = CreateVerticalPanel("MainMenu", rt, 48f);
            StretchFull(_mainMenuPanel.GetComponent<RectTransform>());
            BuildMainMenu(_mainMenuPanel.transform);

            _splitPanel = new GameObject("SplitRoot", typeof(RectTransform));
            _splitPanel.transform.SetParent(rt, false);
            StretchFull(_splitPanel.GetComponent<RectTransform>());
            _splitPanel.SetActive(false);

            var splitRt = _splitPanel.GetComponent<RectTransform>();
            var row = _splitPanel.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.UpperCenter;
            row.spacing = 10f;
            row.padding = new RectOffset(24, 24, 28, 28);
            row.childControlHeight = true;
            row.childControlWidth = true;
            row.childForceExpandHeight = true;
            row.childForceExpandWidth = true;

            _leftColumn = CreateColumn("MenusColumn", splitRt, 1f, "Menus", true);
            var divider = CreateUiObject<Image>("Divider", splitRt);
            var divLe = divider.gameObject.AddComponent<LayoutElement>();
            divLe.preferredWidth = 4f;
            divLe.flexibleWidth = 0f;
            divider.rectTransform.sizeDelta = new Vector2(4f, 0f);
            divider.color = new Color(1f, 1f, 1f, 0.35f);
            divider.raycastTarget = false;
            _rightColumn = CreateColumn("ToolsColumn", splitRt, 1f, "Tools & Inventory", false);
        }

        GameObject CreateColumn(string name, RectTransform parent, float flexW, string header, bool menusColumn)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.flexibleWidth = flexW;
            le.minWidth = 160f;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.13f, 0.16f, 0.25f);
            img.raycastTarget = false;

            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 12, 12);
            v.spacing = 10f;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandWidth = true;

            AddHeader(go.transform, header);
            if (menusColumn)
            {
                AddPlaceholderButton(go.transform, "Scenario", null);
                AddPlaceholderButton(go.transform, "Training", null);
                AddPlaceholderButton(go.transform, "Journal", null);
                AddPlaceholderButton(go.transform, "Back to menu", OnBackToMenuClicked);
            }
            else
            {
                AddPlaceholderButton(go.transform, "Tool A", null);
                AddPlaceholderButton(go.transform, "Tool B", null);
                AddSectionLabel(go.transform, "Inventory");
                AddPlaceholderButton(go.transform, "Slot 1 (empty)", null);
                AddPlaceholderButton(go.transform, "Slot 2 (empty)", null);
                AddPlaceholderButton(go.transform, "Back to menu", OnBackToMenuClicked);
            }

            return go;
        }

        void BuildMainMenu(Transform parent)
        {
            AddHeader(parent, "RRX");
            AddPlaceholderButton(parent, "Start", OnStartClicked);
            AddPlaceholderButton(parent, "Settings", OnSettingsClicked);
            AddPlaceholderButton(parent, "Help", OnHelpClicked);
            AddPlaceholderButton(parent, "Exit", OnExitClicked);
        }

        static void AddHeader(Transform parent, string text)
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 56f;
            le.preferredHeight = 56f;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 36;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 1f, 1f, 0.95f);
        }

        static void AddSectionLabel(Transform parent, string text)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 32f;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = new Color(0.9f, 0.92f, 0.95f, 0.9f);
        }

        void OnBackToMenuClicked()
        {
            if (_splitPanel != null)
                _splitPanel.SetActive(false);
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(true);
        }

        static void AddPlaceholderButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "_Btn", typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 52f;
            le.preferredHeight = 52f;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.25f, 0.28f, 0.34f, ButtonBackdropAlpha);
            var btn = go.GetComponent<Button>();
            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(go.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (onClick != null)
                btn.onClick.AddListener(onClick);
        }

        static GameObject CreateVerticalPanel(string name, RectTransform parent, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(40, 40, 36, 36);
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandWidth = true;
            return go;
        }

        static T CreateUiObject<T>(string name, RectTransform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
        }

        void OnStartClicked()
        {
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(false);
            if (_splitPanel != null)
                _splitPanel.SetActive(true);
        }

        void OnSettingsClicked()
        {
            Debug.Log("[RRX HUD] Settings (placeholder).");
        }

        void OnHelpClicked()
        {
            Debug.Log("[RRX HUD] Help (placeholder).");
        }

        void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
