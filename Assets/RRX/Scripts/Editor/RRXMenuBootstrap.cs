using UnityEditor;
using UnityEngine;

/// <summary>
/// Minimal menu (no extra package references). If you see <b>Window → RRX</b>, the Editor pipeline is OK.
/// </summary>
static class RRXMenuBootstrap
{
    const string Prefix = "Window/RRX/";

    [MenuItem(Prefix + "About RRX Tools", false, 0)]
    static void About()
    {
        EditorUtility.DisplayDialog(
            "RRX",
            "If this dialog appears, RRX Editor scripts are loading.\n\n" +
            "Use:\n• RRX → Build Complete MR Scene (Auto)\n" +
            "• Or Window → RRX → Build Complete MR Scene (Auto)\n\n" +
            "If you do not see any RRX entries, open Console (Ctrl/Cmd+Shift+C) and fix red compile errors first.",
            "OK");
    }
}
