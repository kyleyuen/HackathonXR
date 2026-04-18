using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace RRX.Locomotion
{
    /// <summary>
    /// Forward/back only from the <b>left</b> thumbstick Y axis (no strafe).
    /// </summary>
    public class RRXTankForwardMoveProvider : ContinuousMoveProviderBase
    {
        [SerializeField]
        InputActionProperty m_LeftHandMoveAction;

        public InputActionProperty leftHandMoveAction
        {
            get => m_LeftHandMoveAction;
            set => m_LeftHandMoveAction = value;
        }

        protected void OnEnable()
        {
            m_LeftHandMoveAction.EnableDirectAction();
        }

        protected void OnDisable()
        {
            m_LeftHandMoveAction.DisableDirectAction();
        }

        protected override Vector2 ReadInput()
        {
            var v = m_LeftHandMoveAction.action?.ReadValue<Vector2>() ?? Vector2.zero;
            return new Vector2(0f, v.y);
        }
    }
}
