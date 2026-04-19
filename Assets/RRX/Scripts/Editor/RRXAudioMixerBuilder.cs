using RRX.Environment;
using RRX.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace RRX.Editor
{
    static class RRXAudioMixerBuilder
    {
        const string FolderPath = "Assets/RRX/Audio";
        const string MixerPath = "Assets/RRX/Audio/RRX_Mixer.mixer";

        [MenuItem("RRX/Ensure RRX Audio Mixer", false, 49)]
        [MenuItem("Window/RRX/Ensure RRX Audio Mixer", false, 49)]
        static void EnsureMixerMenu() => EnsureMixerAndWireScene();

        public static void EnsureMixerAndWireScene()
        {
            EnsureFolder(FolderPath);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                mixer = ScriptableObject.CreateInstance<AudioMixer>();
                AssetDatabase.CreateAsset(mixer, MixerPath);
                AssetDatabase.SaveAssets();
            }

            var groups = mixer.FindMatchingGroups("Master");
            var master = groups != null && groups.Length > 0 ? groups[0] : null;
            if (master == null)
                return;

            WireGroupInScene<RRXEmergencyAmbience>("_ambienceMixerGroup", master);
            WireGroupInScene<RRXScenarioFeedback>("_sfxMixerGroup", master);
            WireGroupInScene<RRXMallCrowd>("_ambienceMixerGroup", master);
            WireGroupInScene<RRXPatientBreathAudio>("_patientMixerGroup", master);
        }

        static void WireGroupInScene<T>(string fieldName, AudioMixerGroup group) where T : Component
        {
            foreach (var comp in Object.FindObjectsOfType<T>(true))
            {
                var so = new SerializedObject(comp);
                var prop = so.FindProperty(fieldName);
                if (prop == null)
                    continue;
                prop.objectReferenceValue = group;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(comp);
            }
        }

        static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;
            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
