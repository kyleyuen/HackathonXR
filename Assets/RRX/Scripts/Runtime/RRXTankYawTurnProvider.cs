using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace RRX.Locomotion
{
    /// <summary>
    /// Smooth yaw from the <b>right</b> thumbstick horizontal axis only.
    /// Uses <see cref="ContinuousTurnProviderBase"/> cardinal handling (east/west).
    /// </summary>
    public class RRXTankYawTurnProvider : ContinuousTurnProviderBase
    {
        [SerializeField]
        InputActionProperty m_RightHandMoveAction;

        public InputActionProperty rightHandMoveAction
        {
            get => m_RightHandMoveAction;
            set => m_RightHandMoveAction = value;
        }

        protected void OnEnable()
        {
            m_RightHandMoveAction.EnableDirectAction();
        }

        protected void OnDisable()
        {
            m_RightHandMoveAction.DisableDirectAction();
        }

        protected override Vector2 ReadInput()
        {
            var v = m_RightHandMoveAction.action?.ReadValue<Vector2>() ?? Vector2.zero;
            return new Vector2(v.x, 0f);
        }
    }
}
