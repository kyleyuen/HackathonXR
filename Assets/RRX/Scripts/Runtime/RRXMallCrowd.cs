using RRX.Core;
using RRX.Runtime;
using UnityEngine;
using UnityEngine.Audio;
using Unity.XR.CoreUtils;

namespace RRX.Environment
{
    /// <summary>
    /// Simple capsule "shoppers" wandering on the plaza disc; renderers disable beyond <see cref="_hideDistanceMeters"/>
    /// and re-enable when the camera is closer (hysteresis via <see cref="_showDistanceMeters"/>).
    /// Reacts to <see cref="ScenarioRunner"/> state changes: rubberneck, back away, or scatter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RRXMallCrowd : MonoBehaviour
    {
        enum CrowdMode
        {
            Wander,
            Rubberneck,
            BackAway,
            Scatter,
        }

        [SerializeField] int _pedestrianCount = 14;
        [SerializeField] float _minRadiusFromCenter = 1.2f;
        [SerializeField] float _walkSpeedMin = 0.55f;
        [SerializeField] float _walkSpeedMax = 1.05f;
        [SerializeField] float _targetReachEpsilon = 0.35f;
        [SerializeField] float _showDistanceMeters = 16f;
        [SerializeField] float _hideDistanceMeters = 19f;
        [SerializeField] float _playerExclusionRadiusMeters = 0.5f;

        [SerializeField] ScenarioRunner _runner;
        [SerializeField] AudioMixerGroup _ambienceMixerGroup;

        Transform _camera;
        Pedestrian[] _peds;
        Material _sharedBodyMat;
        AudioSource _ambience;

        float _crowdAmbienceVolume = 0.4f;
        bool _crowdAmbienceEnabled = true;
        CrowdMode _mode = CrowdMode.Wander;
        Vector3 _patientPos;
        PatientPresenter _patientPresenter;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        struct Pedestrian
        {
            public Transform Root;
            public Renderer[] Renderers;
            public Vector3 Target;
            public float Speed;
        }

        void Awake()
        {
            if (Application.isPlaying)
            {
                if (PlayerPrefs.HasKey("rrx_crowd_amb_vol"))
                    _crowdAmbienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("rrx_crowd_amb_vol"));
                if (PlayerPrefs.HasKey("rrx_crowd_amb_en"))
                    _crowdAmbienceEnabled = PlayerPrefs.GetInt("rrx_crowd_amb_en") != 0;
                if (PlayerPrefs.HasKey("rrx_crowd_clear"))
                    _playerExclusionRadiusMeters = Mathf.Clamp(PlayerPrefs.GetFloat("rrx_crowd_clear"), 0.05f, 10f);
                if (PlayerPrefs.HasKey("rrx_crowd_show_lod"))
                    _showDistanceMeters = Mathf.Max(1f, PlayerPrefs.GetFloat("rrx_crowd_show_lod"));
                if (PlayerPrefs.HasKey("rrx_crowd_hide_lod"))
                    _hideDistanceMeters = Mathf.Max(_showDistanceMeters + 0.25f, PlayerPrefs.GetFloat("rrx_crowd_hide_lod"));
            }

            _ambience = GetComponent<AudioSource>();
            if (_ambience == null)
                _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.loop = true;
            _ambience.playOnAwake = false;
            _ambience.spatialBlend = 0.35f;
            _ambience.dopplerLevel = 0f;
            _ambience.minDistance = 2f;
            _ambience.maxDistance = 12f;
            _ambience.rolloffMode = AudioRolloffMode.Linear;
            _ambience.priority = 128;
            if (_ambienceMixerGroup != null)
                _ambience.outputAudioMixerGroup = _ambienceMixerGroup;
        }

        void OnEnable()
        {
            EnsureAmbienceClipAndPlay();
        }

        void OnDisable()
        {
            if (_ambience != null)
                _ambience.Stop();
        }

        void Start()
        {
            var oxr = FindObjectOfType<XROrigin>();
            _camera = oxr != null && oxr.Camera != null ? oxr.Camera.transform : Camera.main != null ? Camera.main.transform : null;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _sharedBodyMat = new Material(shader);

            float maxR = Mathf.Max(0.5f, RRXPlayArea.RadiusMeters - 0.4f);
            float minR = Mathf.Clamp(_minRadiusFromCenter, 0.2f, maxR * 0.95f);
            var playerXZ = PlayerXZ();
            var exR = EffectiveExclusionRadius(maxR);

            _peds = new Pedestrian[Mathf.Max(0, _pedestrianCount)];

            if (_runner == null)
                _runner = FindObjectOfType<ScenarioRunner>();

            if (_runner != null)
                _runner.OnStateChanged.AddListener(OnScenarioStateChanged);
            RefreshCrowdModeFromRunner();

            if (_patientPresenter == null)
                _patientPresenter = FindObjectOfType<PatientPresenter>();

            for (var i = 0; i < _peds.Length; i++)
            {
                var root = new GameObject($"Pedestrian_{i + 1}");
                root.transform.SetParent(transform, false);
                root.transform.position = RandomDiscPoint(minR, maxR, playerXZ, exR);

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                body.transform.localScale = new Vector3(0.38f, 0.45f, 0.38f);
                Destroy(body.GetComponent<Collider>());

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(root.transform, false);
                head.transform.localPosition = new Vector3(0f, 1.52f, 0f);
                head.transform.localScale = Vector3.one * 0.22f;
                Destroy(head.GetComponent<Collider>());

                var hue = (i * 0.09f + 0.03f) % 1f;
                var c = Color.HSVToRGB(hue, 0.22f, 0.72f);

                var br = body.GetComponent<Renderer>();
                var hr = head.GetComponent<Renderer>();
                ApplyColor(br, c * 0.95f);
                ApplyColor(hr, c * 1.05f);

                _peds[i] = new Pedestrian
                {
                    Root = root.transform,
                    Renderers = new[] { br, hr },
                    Target = RandomDiscPoint(minR, maxR, playerXZ, exR),
                    Speed = Random.Range(_walkSpeedMin, _walkSpeedMax),
                };
            }

            EnsureAmbienceClipAndPlay();
        }

        void OnDestroy()
        {
            if (_runner != null)
                _runner.OnStateChanged.RemoveListener(OnScenarioStateChanged);
        }

        void OnScenarioStateChanged(ScenarioState state) => RefreshCrowdModeFromRunner();

        void RefreshCrowdModeFromRunner()
        {
            if (_runner == null)
            {
                _mode = CrowdMode.Wander;
                return;
            }

            _mode = ModeForScenarioState(_runner.CurrentState);
        }

        static CrowdMode ModeForScenarioState(ScenarioState state)
        {
            switch (state)
            {
                case ScenarioState.CriticalFailure:
                    return CrowdMode.Scatter;
                case ScenarioState.Recovery:
                case ScenarioState.SceneSafety:
                case ScenarioState.Arrival:
                    return CrowdMode.Wander;
                case ScenarioState.RecoveryPosition:
                    return CrowdMode.BackAway;
                default:
                    return CrowdMode.Rubberneck;
            }
        }

        void EnsureAmbienceClipAndPlay()
        {
            if (_ambience == null)
                return;

            ApplyCrowdAmbience();
        }

        void PersistCrowdAmbiencePrefs()
        {
            if (!Application.isPlaying)
                return;
            PlayerPrefs.SetFloat("rrx_crowd_amb_vol", _crowdAmbienceVolume);
            PlayerPrefs.SetInt("rrx_crowd_amb_en", _crowdAmbienceEnabled ? 1 : 0);
            PlayerPrefs.SetFloat("rrx_crowd_clear", _playerExclusionRadiusMeters);
            PlayerPrefs.SetFloat("rrx_crowd_show_lod", _showDistanceMeters);
            PlayerPrefs.SetFloat("rrx_crowd_hide_lod", _hideDistanceMeters);
        }

        void ApplyCrowdAmbience()
        {
            if (_ambience == null)
                return;

            if (!_crowdAmbienceEnabled)
            {
                _ambience.Stop();
                _ambience.volume = 0f;
                return;
            }

            var proc = FindObjectOfType<RRXProceduralAudio>();
            if (FindObjectOfType<RRXEmergencyAmbience>() != null)
            {
                _ambience.Stop();
                _ambience.volume = 0f;
                return;
            }
            if (proc != null && proc.ClipCrowdMurmur != null)
                _ambience.clip = proc.ClipCrowdMurmur;

            _ambience.loop = true;
            _ambience.volume = _crowdAmbienceVolume;
            if (_ambience.clip != null && !_ambience.isPlaying)
                _ambience.Play();
        }

        void ApplyColor(Renderer r, Color c)
        {
            if (r == null)
                return;
            var m = new Material(_sharedBodyMat);
            if (m.HasProperty(BaseColorId))
                m.SetColor(BaseColorId, c);
            else if (m.HasProperty(ColorId))
                m.SetColor(ColorId, c);
            else
                m.color = c;
            r.sharedMaterial = m;
        }

        /// <summary>
        /// Requested personal space, capped so points can still exist on the plaza annulus (radius cannot exceed outer ring).
        /// </summary>
        float EffectiveExclusionRadius(float maxPlazaR)
        {
            var cap = Mathf.Max(0.2f, maxPlazaR - 0.08f);
            return Mathf.Min(_playerExclusionRadiusMeters, cap);
        }

        static Vector3 PlayerXZ()
        {
            var oxr = FindObjectOfType<XROrigin>();
            var cam = oxr != null && oxr.Camera != null ? oxr.Camera.transform : Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return Vector3.zero;
            var p = cam.position;
            return new Vector3(p.x, 0f, p.z);
        }

        static Vector3 RandomDiscPoint(float minR, float maxR, Vector3 playerXZ, float excludeR)
        {
            const int maxAttempts = 48;
            for (var a = 0; a < maxAttempts; a++)
            {
                var ang = Random.Range(0f, Mathf.PI * 2f);
                var rad = Random.Range(minR, maxR);
                var p = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                if (SqrXZDistance(p, playerXZ) >= excludeR * excludeR)
                    return p;
            }

            // Fallback: opposite side of play disc from player (still on annulus if possible).
            var dir = (Vector3.zero - playerXZ);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir = dir.normalized * Mathf.Clamp((minR + maxR) * 0.5f, minR, maxR);
            var q = new Vector3(dir.x, 0f, dir.z);
            if (SqrXZDistance(q, playerXZ) < excludeR * excludeR)
                q = dir.normalized * Mathf.Max(maxR, excludeR + 0.5f);
            return q;
        }

        static Vector3 RandomDiscPointNoExclusion(float minR, float maxR)
        {
            var ang = Random.Range(0f, Mathf.PI * 2f);
            var rad = Random.Range(minR, maxR);
            return new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
        }

        static float SqrXZDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        static void PushOutsidePlayerExclusion(ref Vector3 xz, Vector3 playerXZ, float excludeR)
        {
            var dx = xz.x - playerXZ.x;
            var dz = xz.z - playerXZ.z;
            var r2 = dx * dx + dz * dz;
            var ex2 = excludeR * excludeR;
            if (r2 >= ex2)
                return;
            if (r2 < 1e-8f)
            {
                var ang = Random.Range(0f, Mathf.PI * 2f);
                xz.x = playerXZ.x + Mathf.Cos(ang) * excludeR;
                xz.z = playerXZ.z + Mathf.Sin(ang) * excludeR;
                return;
            }

            var inv = excludeR / Mathf.Sqrt(r2);
            xz.x = playerXZ.x + dx * inv;
            xz.z = playerXZ.z + dz * inv;
        }

        void Update()
        {
            if (_peds == null)
                return;

            if (_patientPresenter == null)
                _patientPresenter = FindObjectOfType<PatientPresenter>();
            _patientPos = _patientPresenter != null ? _patientPresenter.transform.position : Vector3.zero;

            if (_camera == null)
                ResolveCamera();

            var camPos = _camera != null ? _camera.position : Vector3.zero;
            var canCull = _camera != null;
            float show2 = _showDistanceMeters * _showDistanceMeters;
            float hide2 = _hideDistanceMeters * _hideDistanceMeters;

            float maxR = Mathf.Max(0.5f, RRXPlayArea.RadiusMeters - 0.4f);
            float minR = Mathf.Clamp(_minRadiusFromCenter, 0.2f, maxR * 0.95f);
            var playerXZ = canCull ? new Vector3(camPos.x, 0f, camPos.z) : Vector3.zero;
            var exR = EffectiveExclusionRadius(maxR);
            var patientXZ = new Vector3(_patientPos.x, 0f, _patientPos.z);

            for (var i = 0; i < _peds.Length; i++)
            {
                var p = _peds[i];
                if (p.Root == null)
                    continue;

                if (canCull && p.Renderers != null)
                {
                    var d2 = (p.Root.position - camPos).sqrMagnitude;
                    var hide = d2 > hide2;
                    var show = d2 < show2;
                    foreach (var ren in p.Renderers)
                    {
                        if (ren == null)
                            continue;
                        if (ren.enabled && hide)
                            ren.enabled = false;
                        else if (!ren.enabled && show)
                            ren.enabled = true;
                    }
                }

                var xz = p.Root.position;
                xz.y = 0f;
                if (canCull)
                    PushOutsidePlayerExclusion(ref xz, playerXZ, exR);

                var to = p.Target;
                to.y = 0f;
                if (canCull && SqrXZDistance(to, playerXZ) < exR * exR)
                {
                    p.Target = _mode == CrowdMode.Wander
                        ? RandomDiscPoint(minR, maxR, playerXZ, exR)
                        : p.Target; // Don't randomize mid-mode
                }

                to = p.Target;
                to.y = 0f;
                var delta = to - xz;

                if (delta.sqrMagnitude < _targetReachEpsilon * _targetReachEpsilon)
                {
                    // Pick a new target based on mode
                    if (_mode == CrowdMode.Rubberneck)
                    {
                        float ang = Random.Range(0f, Mathf.PI * 2f);
                        float dist = Random.Range(1.8f, 3.0f);
                        p.Target = patientXZ + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                        p.Speed = Random.Range(0.2f, 0.5f);
                    }
                    else if (_mode == CrowdMode.BackAway || _mode == CrowdMode.Scatter)
                    {
                        // Stay near outer edge
                        p.Target = RandomDiscPoint(maxR * 0.7f, maxR, playerXZ, exR);
                        p.Speed = Random.Range(_walkSpeedMin, _walkSpeedMax);
                    }
                    else
                    {
                        p.Target = canCull
                            ? RandomDiscPoint(minR, maxR, playerXZ, exR)
                            : RandomDiscPointNoExclusion(minR, maxR);
                        p.Speed = Random.Range(_walkSpeedMin, _walkSpeedMax);
                    }
                }
                else
                {
                    var step = p.Speed * Time.deltaTime;
                    xz += delta.normalized * Mathf.Min(step, delta.magnitude);
                    if (canCull)
                        PushOutsidePlayerExclusion(ref xz, playerXZ, exR);
                    p.Root.position = new Vector3(xz.x, 0f, xz.z);

                    // In rubberneck mode, face patient when close enough
                    Vector3 faceDir;
                    if (_mode == CrowdMode.Rubberneck && SqrXZDistance(xz, patientXZ) < 4f * 4f)
                        faceDir = (patientXZ - xz).normalized;
                    else if (delta.sqrMagnitude > 0.0001f)
                        faceDir = delta.normalized;
                    else
                        faceDir = Vector3.forward;

                    p.Root.rotation = Quaternion.LookRotation(faceDir, Vector3.up);
                }

                _peds[i] = p;
            }
        }

        void ResolveCamera()
        {
            var oxr = FindObjectOfType<XROrigin>();
            _camera = oxr != null && oxr.Camera != null ? oxr.Camera.transform : Camera.main != null ? Camera.main.transform : null;
        }

        public float PlayerExclusionRadiusMeters
        {
            get => _playerExclusionRadiusMeters;
            set
            {
                _playerExclusionRadiusMeters = Mathf.Clamp(value, 0.05f, 10f);
                PersistCrowdAmbiencePrefs();
            }
        }

        public float CrowdShowDistanceMeters
        {
            get => _showDistanceMeters;
            set
            {
                _showDistanceMeters = Mathf.Max(1f, value);
                if (_hideDistanceMeters < _showDistanceMeters + 0.25f)
                    _hideDistanceMeters = _showDistanceMeters + 0.25f;
                PersistCrowdAmbiencePrefs();
            }
        }

        public float CrowdHideDistanceMeters
        {
            get => _hideDistanceMeters;
            set
            {
                _hideDistanceMeters = Mathf.Max(_showDistanceMeters + 0.25f, value);
                PersistCrowdAmbiencePrefs();
            }
        }

        public bool CrowdAmbienceEnabled
        {
            get => _crowdAmbienceEnabled;
            set
            {
                _crowdAmbienceEnabled = value;
                PersistCrowdAmbiencePrefs();
                ApplyCrowdAmbience();
            }
        }

        public float CrowdAmbienceVolume
        {
            get => _crowdAmbienceVolume;
            set
            {
                _crowdAmbienceVolume = Mathf.Clamp01(value);
                PersistCrowdAmbiencePrefs();
                ApplyCrowdAmbience();
            }
        }
    }
}
