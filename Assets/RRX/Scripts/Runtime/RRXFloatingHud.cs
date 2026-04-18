using TMPro;
using RRX.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RRX.UI
{
    /// <summary>
    /// World-space HUD parented to the XR headset camera: start menu (Start / Settings / Exit / Help),
    /// then two separate side panels (menus | tools). Back returns to the start menu.
    /// In split mode, both triggers (L2/R2) chord toggles panel visibility when not aiming at either panel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RRXFloatingHud : MonoBehaviour
    {
        const float PanelOpacity = 0.2f;
        const float ButtonBackdropAlpha = 0.85f;
        const int CanvasRefPixels = 900;
        const int CanvasRefPixelsY = 560;
        const float TriggerThreshold = 0.82f;
        /// <summary>Docked strip width in canvas pixels (reference resolution); keep modest so panels sit at FOV edges.</summary>
        const float SplitPanelWidthPx = 248f;
        const float SplitEdgeInsetPx = 4f;
        const float SplitVerticalInsetPx = 24f;
        const float SplitPanelOpenYawDegrees = 14f;
        const float SplitPanelOpenPitchDegrees = 5f;

        [SerializeField] float _forwardMeters = 1.12f;
        [SerializeField] float _downMeters = 0.05f;

        bool _hudBuilt;
        bool _canvasCameraAssigned;
        Canvas _canvas;
        CanvasScaler _scaler;
        RectTransform _rootRect;
        Image _backdropImage;
        GameObject _mainMenuPanel;
        GameObject _splitPanel;
        CanvasGroup _splitCanvasGroup;
        RectTransform _leftPanelRect;
        RectTransform _rightPanelRect;

        bool _splitMenuVisible = true;
        bool _bothTriggersHeldLast;

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

        void Update()
        {
            if (_splitPanel == null || !_splitPanel.activeInHierarchy || _leftPanelRect == null || _rightPanelRect == null)
                return;

            var lv = ReadTrigger(XRNode.LeftHand);
            var rv = ReadTrigger(XRNode.RightHand);
            var bothNow = lv >= TriggerThreshold && rv >= TriggerThreshold;
            if (bothNow && !_bothTriggersHeldLast)
            {
                if (!_splitMenuVisible)
                    SetSplitMenuVisible(true);
                else if (!IsAimingAtEitherPanel())
                    SetSplitMenuVisible(false);
            }

            _bothTriggersHeldLast = bothNow;
        }

        static float ReadTrigger(XRNode node)
        {
            var dev = InputDevices.GetDeviceAtXRNode(node);
            if (!dev.isValid || !dev.TryGetFeatureValue(CommonUsages.trigger, out var v))
                return 0f;
            return v;
        }

        bool IsAimingAtEitherPanel()
        {
            if (!_splitMenuVisible)
                return false;

            if (TryGetControllerRay(XRNode.LeftHand, out var rayL))
            {
                if (RayHitsRectTransform(rayL, _leftPanelRect) || RayHitsRectTransform(rayL, _rightPanelRect))
                    return true;
            }

            if (TryGetControllerRay(XRNode.RightHand, out var rayR))
            {
                if (RayHitsRectTransform(rayR, _leftPanelRect) || RayHitsRectTransform(rayR, _rightPanelRect))
                    return true;
            }

            return false;
        }

        static bool TryGetControllerRay(XRNode node, out Ray worldRay)
        {
            worldRay = default;
            var dev = InputDevices.GetDeviceAtXRNode(node);
            if (!dev.isValid)
                return false;
            if (!dev.TryGetFeatureValue(CommonUsages.devicePosition, out var pos))
                return false;
            if (!dev.TryGetFeatureValue(CommonUsages.deviceRotation, out var rot))
                return false;
            worldRay = new Ray(pos, rot * Vector3.forward);
            return true;
        }

        static bool RayHitsRectTransform(Ray worldRay, RectTransform rt)
        {
            if (rt == null)
                return false;

            rt.GetWorldCorners(_cornerScratch);
            var normal = (-rt.forward).normalized;
            var plane = new Plane(normal, _cornerScratch[0]);
            if (!plane.Raycast(worldRay, out var dist))
                return false;

            var world = worldRay.GetPoint(dist);
            var local = rt.InverseTransformPoint(world);
            return rt.rect.Contains(new Vector2(local.x, local.y));
        }

        static readonly Vector3[] _cornerScratch = new Vector3[4];

        void SetSplitMenuVisible(bool visible)
        {
            _splitMenuVisible = visible;
            if (_splitCanvasGroup == null)
                return;

            _splitCanvasGroup.alpha = visible ? 1f : 0f;
            _splitCanvasGroup.interactable = visible;
            _splitCanvasGroup.blocksRaycasts = visible;
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

            _backdropImage = CreateUiObject<Image>("Backdrop", rt);
            StretchFull(_backdropImage.rectTransform);
            _backdropImage.color = new Color(0.06f, 0.07f, 0.09f, PanelOpacity);
            _backdropImage.raycastTarget = true;

            _mainMenuPanel = CreateVerticalPanel("MainMenu", rt, 48f);
            StretchFull(_mainMenuPanel.GetComponent<RectTransform>());
            BuildMainMenu(_mainMenuPanel.transform);

            _splitPanel = new GameObject("SplitRoot", typeof(RectTransform));
            _splitPanel.transform.SetParent(rt, false);
            StretchFull(_splitPanel.GetComponent<RectTransform>());
            _splitPanel.SetActive(false);

            _splitCanvasGroup = _splitPanel.AddComponent<CanvasGroup>();

            var splitRt = _splitPanel.GetComponent<RectTransform>();
            _leftPanelRect = CreatePanelColumn(splitRt, "MenusPanel", true, dockLeft: true).GetComponent<RectTransform>();
            _rightPanelRect = CreatePanelColumn(splitRt, "ToolsPanel", false, dockLeft: false).GetComponent<RectTransform>();

            // Outward “double window” skew (mirrored): inverted from the previous tilt/yaw so each sash opens the other way.
            _leftPanelRect.localRotation = Quaternion.Euler(-SplitPanelOpenPitchDegrees, -SplitPanelOpenYawDegrees, 0f);
            _rightPanelRect.localRotation = Quaternion.Euler(-SplitPanelOpenPitchDegrees, SplitPanelOpenYawDegrees, 0f);
        }

        GameObject CreatePanelColumn(RectTransform parent, string name, bool menusColumn, bool dockLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (dockLeft)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(SplitEdgeInsetPx, 0f);
                rt.sizeDelta = new Vector2(SplitPanelWidthPx, -SplitVerticalInsetPx * 2f);
            }
            else
            {
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-SplitEdgeInsetPx, 0f);
                rt.sizeDelta = new Vector2(SplitPanelWidthPx, -SplitVerticalInsetPx * 2f);
            }

            var le = go.GetComponent<LayoutElement>();
            le.minWidth = SplitPanelWidthPx;
            le.preferredWidth = SplitPanelWidthPx;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.06f, 0.07f, 0.09f, PanelOpacity);
            img.raycastTarget = true;

            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 12, 12);
            v.spacing = 10f;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandWidth = true;

            AddHeader(go.transform, menusColumn ? "Menus" : "Tools & Inventory");
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
            SetSplitMenuVisible(true);
            if (_splitPanel != null)
                _splitPanel.SetActive(false);
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(true);
            if (_backdropImage != null)
            {
                _backdropImage.enabled = true;
                _backdropImage.raycastTarget = true;
            }
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
            SetSplitMenuVisible(true);
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(false);
            if (_splitPanel != null)
                _splitPanel.SetActive(true);
            if (_backdropImage != null)
            {
                _backdropImage.enabled = false;
                _backdropImage.raycastTarget = false;
            }
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
