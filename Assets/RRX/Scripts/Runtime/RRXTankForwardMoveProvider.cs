using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace RRX.Locomotion
{
    /// <summary>
    /// <b>Left</b> thumbstick full 360° planar move (forward/back + strafe + diagonals).
    /// XRIT computes motion relative to the camera / HMD forward on the floor,
    /// so physically turning your head redirects walk without rotating the rig.
    /// Rig yaw changes only via <see cref="RRXTankYawTurnProvider"/> or tracking.
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
            return m_LeftHandMoveAction.action?.ReadValue<Vector2>() ?? Vector2.zero;
        }
    }
}
