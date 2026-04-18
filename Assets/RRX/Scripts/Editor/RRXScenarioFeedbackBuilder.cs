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
            Undo.AddComponent<RRXProceduralAudio>(go);
            Undo.AddComponent<RRXScenarioFeedback>(go);
            return go;
        }
    }
}
