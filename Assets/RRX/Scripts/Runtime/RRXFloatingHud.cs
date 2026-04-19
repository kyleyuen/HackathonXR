using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using RRX.Core;
using RRX.Environment;
using RRX.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RRX.UI
{
    /// <summary>
    /// World-space HUD parented to the XR headset camera: main menu, split edge panels, settings (HUD + mall
    /// crowd visuals and ambience), help, scenario status / reset, training tips, and tool slot messages.
    /// L2+R2 chord toggles split visibility when no other overlay is open.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RRXFloatingHud : MonoBehaviour
    {
        const string PatientRootName = "RRX_Patient";
        const string PrefHudForward = "rrx_hud_forward";
        const string PrefHudDown = "rrx_hud_down";
        const string PrefPatientSpawnDistance = "rrx_patient_spawn_distance";

        const float PanelOpacity = 0.2f;
        const float ButtonBackdropAlpha = 0.85f;
        const int CanvasRefPixels = 900;
        const int CanvasRefPixelsY = 560;
        const float TriggerThreshold = 0.82f;
        const float SplitPanelWidthPx = 248f;
        const float SplitEdgeInsetPx = 4f;
        const float SplitVerticalInsetPx = 24f;
        const float SplitPanelOpenYawDegrees = 14f;
        const float SplitPanelOpenPitchDegrees = 5f;

        [SerializeField] float _forwardMeters = 2.24f;
        [SerializeField] float _downMeters = 0.05f;
        [SerializeField] float _patientSpawnDistanceMeters = 1.35f;

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

        GameObject _settingsOverlay;
        GameObject _helpOverlay;
        GameObject _scenarioOverlay;
        GameObject _trainingOverlay;
        GameObject _toastOverlay;
        TextMeshProUGUI _toastText;
        TextMeshProUGUI _scenarioStatusText;
        TextMeshProUGUI _scenarioDetailText;
        ScenarioRunner _scenarioRunner;

        bool _splitMenuVisible = true;
        bool _bothTriggersHeldLast;
        bool _settingsReturnToSplit;
        RRXMallCrowd _mallCrowd;
        readonly List<Action> _settingsRefreshers = new List<Action>();

        void Awake()
        {
            if (_hudBuilt)
                return;

            LoadHudPrefs();
            EnsureEventSystem();
            BuildHud();
            _mallCrowd = FindMallCrowdAny();
            _hudBuilt = true;
        }

        static RRXMallCrowd FindMallCrowdAny()
        {
            var list = FindObjectsByType<RRXMallCrowd>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return list != null && list.Length > 0 ? list[0] : null;
        }

        void LoadHudPrefs()
        {
            if (!Application.isPlaying)
                return;
            if (PlayerPrefs.HasKey(PrefHudForward))
                _forwardMeters = Mathf.Clamp(PlayerPrefs.GetFloat(PrefHudForward), 0.35f, 4f);
            if (PlayerPrefs.HasKey(PrefHudDown))
                _downMeters = Mathf.Clamp(PlayerPrefs.GetFloat(PrefHudDown), 0f, 0.45f);
            if (PlayerPrefs.HasKey(PrefPatientSpawnDistance))
            {
                _patientSpawnDistanceMeters = Mathf.Clamp(
                    PlayerPrefs.GetFloat(PrefPatientSpawnDistance), 0.8f, 3f);
            }
        }

        void SaveHudPrefs()
        {
            if (!Application.isPlaying)
                return;
            PlayerPrefs.SetFloat(PrefHudForward, _forwardMeters);
            PlayerPrefs.SetFloat(PrefHudDown, _downMeters);
            PlayerPrefs.SetFloat(PrefPatientSpawnDistance, _patientSpawnDistanceMeters);
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

            if (AnyModalOverlayOpen())
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

        bool AnyModalOverlayOpen()
        {
            return _settingsOverlay != null && _settingsOverlay.activeSelf
                || _helpOverlay != null && _helpOverlay.activeSelf
                || _scenarioOverlay != null && _scenarioOverlay.activeSelf
                || _trainingOverlay != null && _trainingOverlay.activeSelf
                || _toastOverlay != null && _toastOverlay.activeSelf;
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
            {
                ApplyHudLocalPosition();
                return;
            }

            transform.SetParent(cam.transform, false);
            ApplyHudLocalPosition();
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        void ApplyHudLocalPosition()
        {
            transform.localPosition = new Vector3(0f, -_downMeters, _forwardMeters);
        }

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

            _leftPanelRect.localRotation = Quaternion.Euler(-SplitPanelOpenPitchDegrees, -SplitPanelOpenYawDegrees, 0f);
            _rightPanelRect.localRotation = Quaternion.Euler(-SplitPanelOpenPitchDegrees, SplitPanelOpenYawDegrees, 0f);

            BuildSettingsOverlay(rt);
            BuildHelpOverlay(rt);
            BuildScenarioOverlay(rt);
            BuildTrainingOverlay(rt);
            BuildToastOverlay(rt);
        }

        GameObject CreatePanelColumn(RectTransform parent, string name, bool menusColumn, bool dockLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            go.transform.SetParent(parent, false);
            var rtt = go.GetComponent<RectTransform>();
            if (dockLeft)
            {
                rtt.anchorMin = new Vector2(0f, 0f);
                rtt.anchorMax = new Vector2(0f, 1f);
                rtt.pivot = new Vector2(0f, 0.5f);
                rtt.anchoredPosition = new Vector2(SplitEdgeInsetPx, 0f);
                rtt.sizeDelta = new Vector2(SplitPanelWidthPx, -SplitVerticalInsetPx * 2f);
            }
            else
            {
                rtt.anchorMin = new Vector2(1f, 0f);
                rtt.anchorMax = new Vector2(1f, 1f);
                rtt.pivot = new Vector2(1f, 0.5f);
                rtt.anchoredPosition = new Vector2(-SplitEdgeInsetPx, 0f);
                rtt.sizeDelta = new Vector2(SplitPanelWidthPx, -SplitVerticalInsetPx * 2f);
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
                AddPlaceholderButton(go.transform, "Scenario", OnScenarioClicked);
                AddPlaceholderButton(go.transform, "Training", OnTrainingClicked);
                AddPlaceholderButton(go.transform, "Settings", OnSplitSettingsClicked);
                AddPlaceholderButton(go.transform, "Back to menu", OnBackToMenuClicked);
            }
            else
            {
                AddPlaceholderButton(go.transform, "Demo utility slot 1", OnToolAClicked);
                AddPlaceholderButton(go.transform, "Demo utility slot 2", OnToolBClicked);
                AddSectionLabel(go.transform, "Inventory");
                AddPlaceholderButton(go.transform, "Demo inventory slot 1", OnSlot1Clicked);
                AddPlaceholderButton(go.transform, "Demo inventory slot 2", OnSlot2Clicked);
                AddPlaceholderButton(go.transform, "Back to menu", OnBackToMenuClicked);
            }

            return go;
        }

        void BuildMainMenu(Transform parent)
        {
            AddHeader(parent, "RRX");
            AddSectionLabel(parent, "Tip: L2 + R2 toggles side panels");
            AddPlaceholderButton(parent, "Start", OnStartClicked);
            AddPlaceholderButton(parent, "Settings", OnMainMenuSettingsClicked);
            AddPlaceholderButton(parent, "Help", OnHelpClicked);
            AddPlaceholderButton(parent, "Exit", OnExitClicked);
        }

        void BuildSettingsOverlay(RectTransform root)
        {
            _settingsRefreshers.Clear();
            _settingsOverlay = CreateDimOverlay(root, "SettingsOverlay");
            var card = CreateCenterCard(_settingsOverlay.transform, 580f, 720f);
            var v = card.GetComponent<VerticalLayoutGroup>();
            v.spacing = 10f;
            v.padding = new RectOffset(20, 20, 16, 16);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;

            AddHeader(card.transform, "Settings");
            AddSettingsDescription(card.transform);

            AddNumericStepperRow(card.transform, "HUD distance (m)", 0.35f, 4f, 0.05f,
                () => _forwardMeters,
                x =>
                {
                    _forwardMeters = x;
                    ApplyHudLocalPosition();
                    SaveHudPrefs();
                });

            AddNumericStepperRow(card.transform, "HUD height (down m)", 0f, 0.45f, 0.01f,
                () => _downMeters,
                x =>
                {
                    _downMeters = x;
                    ApplyHudLocalPosition();
                    SaveHudPrefs();
                });

            AddNumericStepperRow(card.transform, "Patient spawn distance (m)", 0.8f, 3f, 0.05f,
                () => _patientSpawnDistanceMeters,
                x =>
                {
                    _patientSpawnDistanceMeters = x;
                    SaveHudPrefs();
                });

            AddPlaceholderButton(card.transform, "Reposition patient in front", RepositionPatientInFront);
            AddPlaceholderButton(card.transform, "Re-anchor world here", OnReanchorWorldClicked);

            AddNumericStepperRow(card.transform, "Crowd clearance (m)", 0.05f, 3f, 0.05f,
                () => _mallCrowd != null ? _mallCrowd.PlayerExclusionRadiusMeters : 0.5f,
                x =>
                {
                    if (_mallCrowd != null)
                        _mallCrowd.PlayerExclusionRadiusMeters = x;
                },
                () => _mallCrowd != null);

            AddNumericStepperRow(card.transform, "Crowd show LOD (m)", 2f, 40f, 0.5f,
                () => _mallCrowd != null ? _mallCrowd.CrowdShowDistanceMeters : 16f,
                x =>
                {
                    if (_mallCrowd != null)
                        _mallCrowd.CrowdShowDistanceMeters = x;
                },
                () => _mallCrowd != null);

            AddNumericStepperRow(card.transform, "Crowd hide LOD (m)", 2.5f, 48f, 0.5f,
                () => _mallCrowd != null ? _mallCrowd.CrowdHideDistanceMeters : 19f,
                x =>
                {
                    if (_mallCrowd != null)
                        _mallCrowd.CrowdHideDistanceMeters = x;
                },
                () => _mallCrowd != null);

            AddNumericStepperRow(card.transform, "Crowd ambience", 0f, 1f, 0.05f,
                () => _mallCrowd != null ? _mallCrowd.CrowdAmbienceVolume : 0.4f,
                x =>
                {
                    if (_mallCrowd != null)
                        _mallCrowd.CrowdAmbienceVolume = x;
                },
                () => _mallCrowd != null);

            AddToggleRow(card.transform, "Crowd sounds", () => _mallCrowd != null && _mallCrowd.CrowdAmbienceEnabled,
                active =>
                {
                    if (_mallCrowd != null)
                        _mallCrowd.CrowdAmbienceEnabled = active;
                },
                () => _mallCrowd != null);

            AddToggleRow(card.transform, "Mall crowd", () => _mallCrowd != null && _mallCrowd.gameObject.activeSelf,
                active =>
                {
                    if (_mallCrowd != null)
                        _mallCrowd.gameObject.SetActive(active);
                },
                () => _mallCrowd != null);

            AddPlaceholderButton(card.transform, "Close", HideSettingsOverlay);
        }

        static void AddSettingsDescription(Transform parent)
        {
            var go = new GameObject("SettingsHint", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 44f;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = "HUD placement, patient repositioning, mall crowd visuals, LOD, personal space, and looping crowd ambience.";
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.85f, 0.88f, 0.92f, 0.88f);
        }

        void OnReanchorWorldClicked()
        {
            RRXWorldAnchorService.AnchorNow();
            ShowToast("World re-anchored around your current position.");
        }

        void RepositionPatientInFront()
        {
            var patient = GameObject.Find(PatientRootName);
            if (patient == null)
            {
                ShowToast("No patient found. Spawn one first, then reposition.");
                return;
            }

            var origin = FindObjectOfType<XROrigin>();
            Transform anchor = origin != null && origin.Camera != null
                ? origin.Camera.transform
                : Camera.main != null ? Camera.main.transform : null;

            if (anchor == null)
            {
                ShowToast("No XR camera found for patient reposition.");
                return;
            }

            Vector3 forwardFlat = anchor.forward;
            forwardFlat.y = 0f;
            if (forwardFlat.sqrMagnitude < 0.0001f)
                forwardFlat = anchor.parent != null ? anchor.parent.forward : Vector3.forward;
            forwardFlat.y = 0f;
            forwardFlat.Normalize();

            var target = anchor.position + forwardFlat * _patientSpawnDistanceMeters;
            target.y = patient.transform.position.y;

            patient.transform.SetPositionAndRotation(target, Quaternion.LookRotation(forwardFlat, Vector3.up));
            ShowToast($"Patient moved in front ({_patientSpawnDistanceMeters:0.00}m).");
        }

        void BuildHelpOverlay(RectTransform root)
        {
            _helpOverlay = CreateDimOverlay(root, "HelpOverlay");
            var card = CreateCenterCard(_helpOverlay.transform, 560f, 440f);
            var v = card.GetComponent<VerticalLayoutGroup>();
            v.spacing = 12f;
            v.padding = new RectOffset(22, 22, 18, 18);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;

            AddHeader(card.transform, "Help");
            var body = new GameObject("HelpBody", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            body.transform.SetParent(card.transform, false);
            var le = body.GetComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            le.minHeight = 220f;
            var tmp = body.GetComponent<TextMeshProUGUI>();
            tmp.text =
                "• <b>Start</b> — opens menus + tools on the sides of your view.\n" +
                "• <b>L2 + R2</b> (both triggers) — hide/show split panels when not aiming at them.\n" +
                "• <b>Settings</b> — HUD distance & height, crowd visuals, ambience level, sounds, clearance, LOD.\n" +
                "• <b>Scenario</b> — view scenario state and reset the overdose training flow when a runner is present.\n" +
                "• <b>Training</b> — quick drills and tips (toast messages) for practice between authored sessions.\n" +
                "• <b>Exit</b> — quit play mode / build.";
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = new Color(0.92f, 0.94f, 0.97f, 0.95f);
            tmp.enableWordWrapping = true;

            AddPlaceholderButton(card.transform, "Close", HideHelpOverlay);
        }

        void BuildScenarioOverlay(RectTransform root)
        {
            _scenarioOverlay = CreateDimOverlay(root, "ScenarioOverlay");
            var card = CreateCenterCard(_scenarioOverlay.transform, 560f, 420f);
            var v = card.GetComponent<VerticalLayoutGroup>();
            if (v == null)
                v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10f;
            v.padding = new RectOffset(20, 20, 16, 16);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;

            AddHeader(card.transform, "Scenario");
            var statusGo = new GameObject("ScenarioStatus", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            statusGo.transform.SetParent(card.transform, false);
            statusGo.GetComponent<LayoutElement>().minHeight = 36f;
            _scenarioStatusText = statusGo.GetComponent<TextMeshProUGUI>();
            _scenarioStatusText.fontSize = 22;
            _scenarioStatusText.alignment = TextAlignmentOptions.Center;
            _scenarioStatusText.color = new Color(0.95f, 0.96f, 1f, 0.96f);

            var detailGo = new GameObject("ScenarioDetail", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            detailGo.transform.SetParent(card.transform, false);
            var dle = detailGo.GetComponent<LayoutElement>();
            dle.minHeight = 140f;
            dle.flexibleHeight = 1f;
            _scenarioDetailText = detailGo.GetComponent<TextMeshProUGUI>();
            _scenarioDetailText.fontSize = 19;
            _scenarioDetailText.alignment = TextAlignmentOptions.TopLeft;
            _scenarioDetailText.color = new Color(0.88f, 0.91f, 0.95f, 0.94f);
            _scenarioDetailText.enableWordWrapping = true;

            var row = new GameObject("ScenarioActions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(card.transform, false);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childForceExpandWidth = false;
            row.AddComponent<LayoutElement>().minHeight = 52f;

            AddWideButton(row.transform, "Refresh", RefreshScenarioPanelUi);
            AddWideButton(row.transform, "Reset scenario", OnScenarioResetClicked);

            AddPlaceholderButton(card.transform, "Close", HideScenarioOverlay);
        }

        void BuildTrainingOverlay(RectTransform root)
        {
            _trainingOverlay = CreateDimOverlay(root, "TrainingOverlay");
            var card = CreateCenterCard(_trainingOverlay.transform, 560f, 400f);
            var v = card.GetComponent<VerticalLayoutGroup>();
            if (v == null)
                v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10f;
            v.padding = new RectOffset(20, 20, 16, 16);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;

            AddHeader(card.transform, "Training");
            var intro = new GameObject("TrainingIntro", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            intro.transform.SetParent(card.transform, false);
            intro.GetComponent<LayoutElement>().minHeight = 56f;
            var introTmp = intro.GetComponent<TextMeshProUGUI>();
            introTmp.text = "Short drills and reminders. Full modules can still be wired to these entry points.";
            introTmp.fontSize = 19;
            introTmp.alignment = TextAlignmentOptions.TopLeft;
            introTmp.color = new Color(0.88f, 0.91f, 0.95f, 0.94f);
            introTmp.enableWordWrapping = true;

            AddPlaceholderButton(card.transform, "Hotspot flow review", OnTrainingHotspotReview);
            AddPlaceholderButton(card.transform, "Rewind checkpoints", OnTrainingRewindTips);
            AddPlaceholderButton(card.transform, "Time pressure (if enabled)", OnTrainingTimePressure);
            AddPlaceholderButton(card.transform, "Close", HideTrainingOverlay);
        }

        static void AddWideButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "_Btn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 168f;
            go.GetComponent<LayoutElement>().minHeight = 48f;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.25f, 0.28f, 0.34f, ButtonBackdropAlpha);
            var btn = go.GetComponent<Button>();
            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(go.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            btn.onClick.AddListener(onClick);
        }

        void BuildToastOverlay(RectTransform root)
        {
            _toastOverlay = CreateDimOverlay(root, "ToastOverlay", 0.35f);
            var card = CreateCenterCard(_toastOverlay.transform, 440f, 160f);
            var v = card.GetComponent<VerticalLayoutGroup>();
            v.spacing = 14f;
            v.padding = new RectOffset(20, 20, 18, 18);
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true;

            var toastGo = new GameObject("ToastText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            toastGo.transform.SetParent(card.transform, false);
            toastGo.GetComponent<LayoutElement>().minHeight = 72f;
            _toastText = toastGo.GetComponent<TextMeshProUGUI>();
            _toastText.fontSize = 22;
            _toastText.alignment = TextAlignmentOptions.Center;
            _toastText.color = Color.white;
            _toastText.enableWordWrapping = true;

            AddPlaceholderButton(card.transform, "OK", HideToast);
            _toastOverlay.SetActive(false);
        }

        static void AddInfoCardContent(Transform cardRoot, string title, string body, UnityEngine.Events.UnityAction onClose)
        {
            var v = cardRoot.GetComponent<VerticalLayoutGroup>();
            if (v == null)
                v = cardRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 12f;
            v.padding = new RectOffset(20, 20, 16, 16);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childForceExpandWidth = true;

            AddHeader(cardRoot, title);
            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            bodyGo.transform.SetParent(cardRoot, false);
            var le = bodyGo.GetComponent<LayoutElement>();
            le.minHeight = 120f;
            le.flexibleHeight = 1f;
            var tmp = bodyGo.GetComponent<TextMeshProUGUI>();
            tmp.text = body;
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = new Color(0.9f, 0.92f, 0.95f, 0.95f);
            tmp.enableWordWrapping = true;

            AddPlaceholderButton(cardRoot, "Close", onClose);
        }

        static GameObject CreateDimOverlay(RectTransform root, string name, float alpha = 0.55f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            StretchFull(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, alpha);
            img.raycastTarget = true;
            go.SetActive(false);
            return go;
        }

        static GameObject CreateCenterCard(Transform overlayParent, float w, float h)
        {
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(overlayParent, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.94f);
            return card;
        }

        void AddNumericStepperRow(Transform parent, string label, float min, float max, float step,
            System.Func<float> read, System.Action<float> write, System.Func<bool> enabledCheck = null)
        {
            var row = new GameObject(label + "_Stepper", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 8f;
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.padding = new RectOffset(0, 0, 4, 4);
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 48f;
            rowLe.preferredHeight = 48f;

            var lab = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            lab.transform.SetParent(row.transform, false);
            var labLe = lab.GetComponent<LayoutElement>();
            labLe.preferredWidth = 220f;
            labLe.minWidth = 200f;
            var labTmp = lab.GetComponent<TextMeshProUGUI>();
            labTmp.text = label;
            labTmp.fontSize = 19;
            labTmp.alignment = TextAlignmentOptions.Left;
            labTmp.color = new Color(0.92f, 0.94f, 0.97f, 0.95f);

            var minus = CreateSmallButton(row.transform, "-");
            var valGo = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            valGo.transform.SetParent(row.transform, false);
            var valLe = valGo.GetComponent<LayoutElement>();
            valLe.preferredWidth = 72f;
            valLe.minWidth = 72f;
            var valTmp = valGo.GetComponent<TextMeshProUGUI>();
            valTmp.fontSize = 20;
            valTmp.alignment = TextAlignmentOptions.Center;
            valTmp.color = Color.white;

            var plus = CreateSmallButton(row.transform, "+");

            void Refresh()
            {
                var on = enabledCheck == null || enabledCheck();
                valTmp.text = on ? read().ToString("0.00") : "—";
                minus.interactable = on;
                plus.interactable = on;
            }

            minus.onClick.AddListener(() =>
            {
                if (enabledCheck != null && !enabledCheck())
                    return;
                write(Mathf.Clamp(read() - step, min, max));
                Refresh();
            });
            plus.onClick.AddListener(() =>
            {
                if (enabledCheck != null && !enabledCheck())
                    return;
                write(Mathf.Clamp(read() + step, min, max));
                Refresh();
            });

            Refresh();
            _settingsRefreshers.Add(Refresh);
        }

        void AddToggleRow(Transform parent, string label, System.Func<bool> read, System.Action<bool> write, System.Func<bool> enabledCheck)
        {
            var row = new GameObject(label + "_Toggle", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleLeft;
            h.spacing = 12f;
            row.AddComponent<LayoutElement>().minHeight = 48f;

            var lab = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            lab.transform.SetParent(row.transform, false);
            lab.GetComponent<LayoutElement>().preferredWidth = 220f;
            var labTmp = lab.GetComponent<TextMeshProUGUI>();
            labTmp.text = label;
            labTmp.fontSize = 19;
            labTmp.alignment = TextAlignmentOptions.Left;
            labTmp.color = new Color(0.92f, 0.94f, 0.97f, 0.95f);

            var btnGo = new GameObject("ToggleBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGo.transform.SetParent(row.transform, false);
            btnGo.GetComponent<LayoutElement>().preferredWidth = 160f;
            btnGo.GetComponent<LayoutElement>().minHeight = 44f;
            var img = btnGo.GetComponent<Image>();
            img.color = new Color(0.25f, 0.28f, 0.34f, ButtonBackdropAlpha);
            var btn = btnGo.GetComponent<Button>();
            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(btnGo.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var btnTmp = txtGo.GetComponent<TextMeshProUGUI>();
            btnTmp.fontSize = 18;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;

            void Refresh()
            {
                var on = enabledCheck();
                btn.interactable = on;
                btnTmp.text = on ? (read() ? "On" : "Off") : "N/A";
            }

            btn.onClick.AddListener(() =>
            {
                if (!enabledCheck())
                    return;
                write(!read());
                Refresh();
            });

            Refresh();
            _settingsRefreshers.Add(Refresh);
        }

        static Button CreateSmallButton(Transform parent, string caption)
        {
            var go = new GameObject(caption + "_Btn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 44f;
            go.GetComponent<LayoutElement>().minWidth = 44f;
            go.GetComponent<LayoutElement>().minHeight = 40f;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.3f, 0.33f, 0.4f, ButtonBackdropAlpha);
            var btn = go.GetComponent<Button>();
            var tgo = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
            tgo.transform.SetParent(go.transform, false);
            StretchFull(tgo.GetComponent<RectTransform>());
            var tmp = tgo.GetComponent<TextMeshProUGUI>();
            tmp.text = caption;
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return btn;
        }

        static void AddHeader(Transform parent, string text)
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 52f;
            le.preferredHeight = 52f;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 34;
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
            // Re-anchor the mall + patient + ambience to wherever the player is actually standing /
            // facing right now, so the scene builds around them even if they've moved since scene load.
            RRXWorldAnchorService.AnchorNow();

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

        void OnMainMenuSettingsClicked()
        {
            ShowSettingsOverlay(returnToSplit: false);
        }

        void OnSplitSettingsClicked()
        {
            ShowSettingsOverlay(returnToSplit: true);
        }

        void ShowSettingsOverlay(bool returnToSplit)
        {
            _mallCrowd = FindMallCrowdAny();
            _settingsReturnToSplit = returnToSplit;
            if (returnToSplit)
            {
                if (_splitPanel != null)
                    _splitPanel.SetActive(false);
            }
            else
            {
                if (_mainMenuPanel != null)
                    _mainMenuPanel.SetActive(false);
            }

            if (_backdropImage != null)
            {
                _backdropImage.enabled = true;
                _backdropImage.raycastTarget = true;
            }

            _settingsOverlay.SetActive(true);
            foreach (var r in _settingsRefreshers)
                r?.Invoke();
        }

        void HideSettingsOverlay()
        {
            _settingsOverlay.SetActive(false);
            if (_settingsReturnToSplit)
            {
                if (_splitPanel != null)
                    _splitPanel.SetActive(true);
                if (_backdropImage != null)
                {
                    _backdropImage.enabled = false;
                    _backdropImage.raycastTarget = false;
                }
            }
            else
            {
                if (_mainMenuPanel != null)
                    _mainMenuPanel.SetActive(true);
                if (_backdropImage != null)
                {
                    _backdropImage.enabled = true;
                    _backdropImage.raycastTarget = true;
                }
            }
        }

        void OnHelpClicked()
        {
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(false);
            if (_backdropImage != null)
            {
                _backdropImage.enabled = true;
                _backdropImage.raycastTarget = true;
            }

            _helpOverlay.SetActive(true);
        }

        void HideHelpOverlay()
        {
            _helpOverlay.SetActive(false);
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(true);
        }

        void OnScenarioClicked()
        {
            if (_splitPanel != null)
                _splitPanel.SetActive(false);
            if (_backdropImage != null)
            {
                _backdropImage.enabled = true;
                _backdropImage.raycastTarget = true;
            }

            _scenarioOverlay.SetActive(true);
            RefreshScenarioPanelUi();
            SubscribeScenarioOverlayLiveUpdates();
        }

        void ResolveScenarioRunner()
        {
            var list = FindObjectsByType<ScenarioRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _scenarioRunner = list != null && list.Length > 0 ? list[0] : null;
        }

        void RefreshScenarioPanelUi()
        {
            if (_scenarioStatusText == null || _scenarioDetailText == null)
                return;

            ResolveScenarioRunner();
            if (_scenarioRunner == null)
            {
                _scenarioStatusText.text = "No scenario runner in this scene";
                _scenarioDetailText.text =
                    "Add a <b>ScenarioRunner</b> (with patient presenter / clock as in your demo) to drive the overdose flow. " +
                    "Hotspots and wrist objectives read from that runner.";
                return;
            }

            _scenarioStatusText.text = $"Current state: <b>{_scenarioRunner.CurrentState}</b>";
            _scenarioDetailText.text =
                $"Next hotspot: <b>{_scenarioRunner.NextRequiredHotspot}</b>\n" +
                $"Next action: <b>{_scenarioRunner.NextRequiredAction}</b>\n" +
                $"Failures: {_scenarioRunner.FailureCount}  •  Rewinds: {_scenarioRunner.RewindCount}\n\n" +
                "Use the authored world hotspots to submit actions. <b>Reset scenario</b> returns to arrival and clears checkpoints.";
        }

        void OnScenarioResetClicked()
        {
            ResolveScenarioRunner();
            if (_scenarioRunner == null)
            {
                ShowToast("No scenario runner found in the scene.");
                return;
            }

            _scenarioRunner.ResetScenario();
            RefreshScenarioPanelUi();
            ShowToast("Scenario reset to scene safety.");
        }

        void OnTrainingHotspotReview()
        {
            ShowToast(
                "Hotspot flow: scene safety scan → check response → open airway → check breathing → call for help → administer naloxone → recovery position.");
        }

        void OnTrainingRewindTips()
        {
            ShowToast(
                "Rewind: use your scenario’s rewind binding to jump to the last good checkpoint after a mistake. Failures escalate visuals until critical failure.");
        }

        void OnTrainingTimePressure()
        {
            ShowToast(
                "Time pressure: if the runner’s clock is enabled, watch for warnings and expiry. You can disable time pressure on the ScenarioRunner in the inspector.");
        }

        void HideScenarioOverlay()
        {
            UnsubscribeScenarioOverlayLiveUpdates();
            _scenarioOverlay.SetActive(false);
            if (_splitPanel != null)
                _splitPanel.SetActive(true);
            if (_backdropImage != null)
            {
                _backdropImage.enabled = false;
                _backdropImage.raycastTarget = false;
            }
        }

        void OnTrainingClicked()
        {
            if (_splitPanel != null)
                _splitPanel.SetActive(false);
            if (_backdropImage != null)
            {
                _backdropImage.enabled = true;
                _backdropImage.raycastTarget = true;
            }

            _trainingOverlay.SetActive(true);
        }

        void SubscribeScenarioOverlayLiveUpdates()
        {
            ResolveScenarioRunner();
            if (_scenarioRunner != null)
                _scenarioRunner.OnStateChanged.AddListener(OnScenarioRunnerStateChangedForOverlay);
        }

        void UnsubscribeScenarioOverlayLiveUpdates()
        {
            if (_scenarioRunner != null)
                _scenarioRunner.OnStateChanged.RemoveListener(OnScenarioRunnerStateChangedForOverlay);
        }

        void OnScenarioRunnerStateChangedForOverlay(ScenarioState _)
        {
            if (_scenarioOverlay != null && _scenarioOverlay.activeSelf)
                RefreshScenarioPanelUi();
        }

        void HideTrainingOverlay()
        {
            _trainingOverlay.SetActive(false);
            if (_splitPanel != null)
                _splitPanel.SetActive(true);
            if (_backdropImage != null)
            {
                _backdropImage.enabled = false;
                _backdropImage.raycastTarget = false;
            }
        }

        void OnToolAClicked()
        {
            ShowToast("Demo utility slot 1 is not wired in this build.");
        }

        void OnToolBClicked()
        {
            ShowToast("Demo utility slot 2 is not wired in this build.");
        }

        void OnSlot1Clicked()
        {
            ShowToast("Demo inventory slot 1 has no item assigned.");
        }

        void OnSlot2Clicked()
        {
            ShowToast("Demo inventory slot 2 has no item assigned.");
        }

        void ShowToast(string message)
        {
            StopAllCoroutines();
            _toastText.text = message;
            _toastOverlay.SetActive(true);
            StartCoroutine(HideToastAfterDelay(4.5f));
        }

        IEnumerator HideToastAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            HideToast();
        }

        void HideToast()
        {
            StopAllCoroutines();
            if (_toastOverlay != null)
                _toastOverlay.SetActive(false);
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
