using RRX.Core;
using RRX.Runtime;
using UnityEditor;
using UnityEngine;

namespace RRX.Editor
{
    static class RRXScenarioFeedbackBuilder
    {
        const string RootName = "RRX_Feedback";

        [MenuItem("RRX/Spawn Scenario Feedback Node", false, 47)]
        [MenuItem("Window/RRX/Spawn Scenario Feedback Node", false, 47)]
        static void MenuSpawn()
        {
            SpawnOrRebuild();
            Debug.Log("[RRX] Scenario feedback node spawned.");
        }

        public static GameObject SpawnOrRebuild()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var go = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(go, "RRX Feedback");
            var src = Undo.AddComponent<AudioSource>(go);
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            var bank = Undo.AddComponent<RRXProceduralAudio>(go);
            var feedback = Undo.AddComponent<RRXScenarioFeedback>(go);

            var runner = Object.FindObjectOfType<ScenarioRunner>();
            if (runner != null)
            {
                var so = new SerializedObject(feedback);
                var runnerProp = so.FindProperty("_runner");
                var bankProp = so.FindProperty("_audioBank");
                if (runnerProp != null)
                    runnerProp.objectReferenceValue = runner;
                if (bankProp != null)
                    bankProp.objectReferenceValue = bank;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feedback);
            }
            return go;
        }
    }
}
