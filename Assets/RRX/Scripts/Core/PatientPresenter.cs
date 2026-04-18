using UnityEngine;

namespace RRX.Core
{
    /// <summary>Maps PatientVisualState to Animator parameters / optional blend shapes.</summary>
    public sealed class PatientPresenter : MonoBehaviour
    {
        public Animator CharacterAnimator;

        static readonly int BreathRateHash = Animator.StringToHash("BreathRate");
        static readonly int ConsciousnessHash = Animator.StringToHash("Consciousness");
        static readonly int CyanosisHash = Animator.StringToHash("Cyanosis");
        static readonly int HeadSlumpHash = Animator.StringToHash("HeadSlump");

        PatientVisualState _current;

        void Awake()
        {
            if (CharacterAnimator == null)
                CharacterAnimator = GetComponentInChildren<Animator>();
        }

        public void Apply(in PatientVisualState state)
        {
            _current = state;
            if (CharacterAnimator == null) return;

            CharacterAnimator.SetFloat(BreathRateHash, state.BreathRate);
            CharacterAnimator.SetFloat(ConsciousnessHash, state.Consciousness);
            CharacterAnimator.SetFloat(CyanosisHash, state.Cyanosis);
            CharacterAnimator.SetFloat(HeadSlumpHash, state.HeadSlump);
        }

        public PatientVisualState Current => _current;
    }
}
