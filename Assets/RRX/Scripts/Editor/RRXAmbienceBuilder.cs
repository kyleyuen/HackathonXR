using RRX.Core;
using RRX.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RRX.Editor
{
    /// <summary>Editor builder that spawns (or rebuilds) the emergency ambience node in the active scene.</summary>
    static class RRXAmbienceBuilder
    {
        const string NodeName = "RRX_Ambience";

        [MenuItem("RRX/Spawn Emergency Ambience", false, 50)]
        [MenuItem("Window/RRX/Spawn Emergency Ambience", false, 50)]
        static void MenuSpawn()
        {
            SpawnOrRebuild();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>Called by <see cref="RRXDemoSceneWizard"/> auto-build pipeline.</summary>
        public static void SpawnOrRebuild()
        {
            var existing = GameObject.Find(NodeName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(NodeName);
            Undo.RegisterCreatedObjectUndo(root, NodeName);

            var runner = Object.FindObjectOfType<ScenarioRunner>();
            var audioNode = GameObject.Find("RRX_Feedback");
            var audio = audioNode != null ? audioNode.GetComponent<RRXProceduralAudio>() : null;

            // Place the ambience node at patient position if possible
            var patient = GameObject.Find(RRXPatientBuilder.RootName);
            if (patient != null)
                root.transform.position = patient.transform.position;

            var ambience = Undo.AddComponent<RRXEmergencyAmbience>(root);

            // Wire references via SerializedObject so the Inspector shows them
            var so = new SerializedObject(ambience);
            var runnerProp = so.FindProperty("_runner");
            var audioProp  = so.FindProperty("_audio");
            if (runnerProp != null && runner != null)
                runnerProp.objectReferenceValue = runner;
            if (audioProp != null && audio != null)
                audioProp.objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ambience);

            Debug.Log("[RRX] Emergency ambience node spawned. Save the scene.");
        }
    }
}
