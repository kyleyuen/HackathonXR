using UnityEngine.XR.Interaction.Toolkit;

namespace RRX.Core
{
    /// <summary>Normalized input payload passed into <see cref="ScenarioRunner.TrySubmit"/>.</summary>
    public readonly struct ScenarioActionSubmission
    {
        public readonly ScenarioAction Action;
        public readonly ScenarioHotspotId HotspotId;
        public readonly XRBaseInteractor Interactor;
        public readonly float SubmittedAtRealtime;

        public ScenarioActionSubmission(
            ScenarioAction action,
            ScenarioHotspotId hotspotId,
            XRBaseInteractor interactor,
            float submittedAtRealtime)
        {
            Action = action;
            HotspotId = hotspotId;
            Interactor = interactor;
            SubmittedAtRealtime = submittedAtRealtime;
        }
    }
}
