using UnityEditor;

namespace RRX.Editor
{
    /// <summary>In-editor checklist for Quest MR smoke test (cannot replace on-device QA).</summary>
    static class RRXDeviceVerifyMenu
    {
        const string Msg =
            "Quest MR smoke test checklist:\n\n" +
            "1) Build Android to Quest 3 (USB or wireless debugging).\n" +
            "2) Confirm passthrough: real room visible; cubes appear over it.\n" +
            "3) After importing com.unity.xr.meta-openxr: Project Settings > XR Plug-in Management > OpenXR > Android > Meta Quest feature group — enable Camera / passthrough features per Unity OpenXR Meta manual.\n" +
            "4) Run RRX > Apply MR Camera Hints To XR Origin (transparent camera clear).\n" +
            "5) Disable HDR on Android if passthrough is black (Quality / Player).\n" +
            "6) Scenario: keys 1/2/3/R (Editor) or grab Phone/Narcan (device).\n\n" +
            "Known: OpenXR .so 16KB alignment warning may persist until Unity/plugin update.";

        [MenuItem("RRX/Quest Device Verify Checklist", false, 100)]
        [MenuItem("Window/RRX/Quest Device Verify Checklist", false, 100)]
        static void ShowChecklist()
        {
            EditorUtility.DisplayDialog("RRX — Quest device verify", Msg, "OK");
        }
    }
}
