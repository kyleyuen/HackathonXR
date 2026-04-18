using RRX.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RRX.UI
{
    /// <summary>
    /// World-space training HUD under <c>RRX_UI_Root</c>: step hints + scenario actions for XR ray interaction.
    /// </summary>
    public sealed class RRXTrainingHudController : MonoBehaviour
    {
        const string BtnCheck = "RRX_Btn_CheckResponsiveness";
        const string BtnCall = "RRX_Btn_Call911";
        const string BtnNarcan = "RRX_Btn_Narcan";
        const string BtnRewind = "RRX_Btn_Rewind";
        const string TextStep = "RRX_Text_Step";
        const string TextHint = "RRX_Text_Hint";

        [SerializeField] ScenarioRunner _runner;

        Button _check;
        Button _call;
        Button _narcan;
        Button _rewind;
        TextMeshProUGUI _step;
        TextMeshProUGUI _hint;

        void Awake()
        {
            if (_runner == null)
                _runner = GetComponentInParent<ScenarioRunner>();

            _check = FindButton(BtnCheck);
            _call = FindButton(BtnCall);
            _narcan = FindButton(BtnNarcan);
            _rewind = FindButton(BtnRewind);
            _step = FindTmp(TextStep);
            _hint = FindTmp(TextHint);

            WireButton(_check, () => _runner?.SubmitAction(ScenarioAction.CheckResponsiveness));
            WireButton(_call, () => _runner?.SubmitAction(ScenarioAction.Call911));
            WireButton(_narcan, () => _runner?.SubmitAction(ScenarioAction.AdministerNarcan));
            WireButton(_rewind, () => _runner?.RewindPreviousCheckpoint());
        }

        void OnEnable()
        {
            if (_runner != null)
            {
                _runner.OnStateChanged.AddListener(OnStateChanged);
                _runner.OnActionHandled.AddListener(OnActionHandled);
            }
        }

        void OnDisable()
        {
            if (_runner != null)
            {
                _runner.OnStateChanged.RemoveListener(OnStateChanged);
                _runner.OnActionHandled.RemoveListener(OnActionHandled);
            }
        }

        void Start()
        {
            FaceBillboardY();
            RefreshUi(_runner != null ? _runner.CurrentState : ScenarioState.Arrival);
        }

        Button FindButton(string childName)
        {
            var t = FindDeep(transform, childName);
            return t != null ? t.GetComponent<Button>() : null;
        }

        TextMeshProUGUI FindTmp(string childName)
        {
            var t = FindDeep(transform, childName);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        static void WireButton(Button b, UnityEngine.Events.UnityAction fn)
        {
            if (b == null || fn == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(fn);
        }

        void OnStateChanged(ScenarioState state)
        {
            RefreshUi(state);
        }

        void OnActionHandled(ScenarioAction action, string reason)
        {
            if (_hint != null && !string.IsNullOrEmpty(reason))
                _hint.text = reason == "ok" ? string.Empty : reason;
        }

        void RefreshUi(ScenarioState state)
        {
            if (_step != null)
                _step.text = StepLabel(state);

            var canRewind = _runner != null && _runner.Snapshots.LastIndex > 0;

            SetScenarioButtons(state);
            if (_rewind != null)
                _rewind.interactable = canRewind;
        }

        void SetScenarioButtons(ScenarioState state)
        {
            if (_check != null)
                _check.interactable = state == ScenarioState.Arrival;
            if (_call != null)
                _call.interactable = state == ScenarioState.CallForHelp;
            if (_narcan != null)
                _narcan.interactable = state == ScenarioState.AdministerNarcan;
        }

        static string StepLabel(ScenarioState state)
        {
            switch (state)
            {
                case ScenarioState.Arrival:
                    return "Step 1 — Check responsiveness (zone or button)";
                case ScenarioState.CallForHelp:
                    return "Step 2 — Call 911 (grab phone or button)";
                case ScenarioState.AdministerNarcan:
                    return "Step 3 — Administer Narcan (grab kit or button)";
                case ScenarioState.Recovery:
                    return "Recovery — scenario complete";
                case ScenarioState.CriticalFailure:
                    return "Critical failure — rewind or restart";
                default:
                    return state.ToString();
            }
        }

        void FaceBillboardY()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            var eye = cam.transform.position;
            var here = transform.position;
            var dir = eye - here;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                return;
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }
}
