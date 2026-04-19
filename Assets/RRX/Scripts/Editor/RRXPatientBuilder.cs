using RRX.Core;
using RRX.Interactions;
using RRX.Runtime;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace RRX.Editor
{
    /// <summary>
    /// Spawns a detailed anatomical blockout patient lying supine on the MR floor in front of the user.
    /// Every scenario hotspot is anchored to a precise landmark pip (tiny emissive sphere) while the
    /// invisible BoxCollider surrounding it stays generous so Quest ray selection remains comfortable.
    /// Wires up <see cref="PatientPresenter"/> to the scene's <see cref="ScenarioRunner"/> automatically.
    /// </summary>
    static class RRXPatientBuilder
    {
        internal const string RootName = "RRX_Patient";
        const string MatFolder = "Assets/RRX/Materials";

        // Patient is placed in the XR rig's local forward: head close to the user, feet extending away.
        const float HeadLocalZ   = 0.85f;
        const float TorsoLocalZ  = 1.30f;
        const float PelvisLocalZ = 1.70f;
        const float ThighLocalZ  = 2.00f;
        const float ShinLocalZ   = 2.40f;
        const float FootLocalZ   = 2.62f;

        [MenuItem("RRX/Spawn Patient In Front Of User", false, 45)]
        [MenuItem("Window/RRX/Spawn Patient In Front Of User", false, 45)]
        static void MenuSpawn()
        {
            var go = SpawnOrRebuildPatient();
            if (go != null)
                Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[RRX] Detailed patient + hotspot pips spawned in front of the XR rig. Save the scene.");
        }

        /// <summary>Called by the demo scene wizard so the auto-build pipeline always spawns a patient.</summary>
        public static GameObject SpawnOrRebuildPatient()
        {
            var origin = Object.FindObjectOfType<XROrigin>();
            Vector3 rigPos = origin != null ? origin.transform.position : Vector3.zero;
            Quaternion rigRot = origin != null ? origin.transform.rotation : Quaternion.identity;

            var existing = GameObject.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "RRX Patient");
            root.transform.SetPositionAndRotation(rigPos, rigRot);

            // ── materials ────────────────────────────────────────────────────
            var skin      = GetOrCreateMat("RRX_Mat_PatientSkin",     new Color(0.82f, 0.70f, 0.62f));
            var skinDark  = GetOrCreateMat("RRX_Mat_PatientSkinDark", new Color(0.68f, 0.54f, 0.46f));
            var shirt     = GetOrCreateMat("RRX_Mat_PatientShirt",    new Color(0.35f, 0.48f, 0.60f));
            var ribMat    = GetOrCreateMat("RRX_Mat_PatientRib",      new Color(0.42f, 0.55f, 0.68f));
            var pants     = GetOrCreateMat("RRX_Mat_PatientPants",    new Color(0.20f, 0.24f, 0.30f));
            var hair      = GetOrCreateMat("RRX_Mat_PatientHair",     new Color(0.12f, 0.10f, 0.09f));
            var shoe      = GetOrCreateMat("RRX_Mat_PatientShoe",     new Color(0.08f, 0.08f, 0.09f));
            var lips      = GetOrCreateMat("RRX_Mat_PatientLips",     new Color(0.72f, 0.35f, 0.38f));
            var eyelidMat = GetOrCreateMat("RRX_Mat_PatientEyelid",   new Color(0.70f, 0.58f, 0.50f));
            var belt      = GetOrCreateMat("RRX_Mat_PatientBelt",     new Color(0.12f, 0.09f, 0.06f));
            var sockMat   = GetOrCreateMat("RRX_Mat_PatientSock",     new Color(0.86f, 0.86f, 0.84f));
            var phoneMat  = GetOrCreateMat("RRX_Mat_Phone",           new Color(0.05f, 0.08f, 0.12f));
            var pipMat    = GetOrCreatePipMat();

            // ── body ─────────────────────────────────────────────────────────
            BuildDetailedHead(root.transform, skin, skinDark, lips, eyelidMat, hair);
            BuildNeck(root.transform, skin);
            BuildDetailedTorso(root.transform, shirt, ribMat, skinDark);
            BuildPelvis(root.transform, pants, belt);
            BuildDetailedArm(root.transform, shirt, skin, skinDark, isLeft: true);
            BuildDetailedArm(root.transform, shirt, skin, skinDark, isLeft: false);
            BuildDetailedLeg(root.transform, pants, skinDark, sockMat, shoe, isLeft: true);
            BuildDetailedLeg(root.transform, pants, skinDark, sockMat, shoe, isLeft: false);

            // ── components ───────────────────────────────────────────────────
            var presenter  = root.AddComponent<PatientPresenter>();
            Undo.RegisterCreatedObjectUndo(presenter, "RRX Patient Presenter");
            var procedural = root.AddComponent<RRXPatientProceduralVisuals>();
            Undo.RegisterCreatedObjectUndo(procedural, "RRX Patient Procedural Visuals");

            var runner = Object.FindObjectOfType<ScenarioRunner>();
            WirePresenterToRunner(runner, presenter);

            // ── hotspots ─────────────────────────────────────────────────────
            // SceneScan: invisible discovery zone that covers the whole patient
            BuildHotspot(root.transform, "Hotspot_SceneScan",
                localCenter: new Vector3(0f, 0.35f, TorsoLocalZ),
                localSize:   new Vector3(1.8f, 1.0f, 2.4f),
                material:    null,
                hotspotId:   ScenarioHotspotId.SceneScan,
                runner:      runner,
                visible:     false);

            // Each pip sits exactly on the named anatomical landmark.
            // The invisible BoxCollider (colliderSize) is kept generous for comfortable Quest aiming.
            const float Pip = 0.02f; // visual pip radius (m)

            BuildHotspotWithPip(root.transform, "Hotspot_Shoulder",
                localCenter:   new Vector3(-0.24f, 0.22f, TorsoLocalZ - 0.20f),
                pipRadius:     Pip,
                colliderSize:  new Vector3(0.22f, 0.22f, 0.22f),
                pipMat:        pipMat,
                hotspotId:     ScenarioHotspotId.Shoulder,
                runner:        runner);

            BuildHotspotWithPip(root.transform, "Hotspot_Chin",
                localCenter:   new Vector3(0f, 0.04f, HeadLocalZ + 0.12f),
                pipRadius:     Pip,
                colliderSize:  new Vector3(0.17f, 0.15f, 0.15f),
                pipMat:        pipMat,
                hotspotId:     ScenarioHotspotId.Chin,
                runner:        runner);

            BuildHotspotWithPip(root.transform, "Hotspot_Mouth",
                localCenter:   new Vector3(0f, 0.14f, HeadLocalZ + 0.08f),
                pipRadius:     Pip,
                colliderSize:  new Vector3(0.17f, 0.14f, 0.14f),
                pipMat:        pipMat,
                hotspotId:     ScenarioHotspotId.Mouth,
                runner:        runner);

            BuildHotspotWithPip(root.transform, "Hotspot_NasalNarcan",
                localCenter:   new Vector3(0f, 0.26f, HeadLocalZ + 0.04f),
                pipRadius:     Pip,
                colliderSize:  new Vector3(0.15f, 0.15f, 0.15f),
                pipMat:        pipMat,
                hotspotId:     ScenarioHotspotId.Nose,
                runner:        runner);

            BuildHotspotWithPip(root.transform, "Hotspot_Hip",
                localCenter:   new Vector3(0.23f, 0.24f, PelvisLocalZ - 0.03f),
                pipRadius:     Pip,
                colliderSize:  new Vector3(0.22f, 0.20f, 0.20f),
                pipMat:        pipMat,
                hotspotId:     ScenarioHotspotId.Hip,
                runner:        runner);

            BuildPhone(root.transform, phoneMat, runner);
            BuildDecoys(root.transform, runner);

            return root;
        }

        // ── ANATOMY BUILDERS ────────────────────────────────────────────────

        /// <summary>
        /// Creates an empty "Head" parent at the skull pivot so the head-slump rotation animates all
        /// face features together. Face is pointing upward (+Y) as the patient is lying supine.
        /// </summary>
        static void BuildDetailedHead(Transform root, Material skin, Material skinDark, Material lips,
            Material eyelid, Material hair)
        {
            var headGO = new GameObject("Head");
            headGO.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(headGO, "Head");
            headGO.transform.localPosition = new Vector3(0f, 0.13f, HeadLocalZ);
            var h = headGO.transform;

            // Skull — main cranial mass
            BuildSphere(h, "Skull",      skin,     Vector3.zero,                              0.120f);
            // Jaw — lower face block
            BuildCube  (h, "Jaw",        skin,     new Vector3(0f,   -0.060f,  0.040f),  new Vector3(0.18f,  0.070f, 0.160f), Quaternion.Euler(8f, 0f, 0f));
            // Chin — bottom-front tip (landmark anchor)
            BuildCube  (h, "Chin",       skin,     new Vector3(0f,   -0.090f,  0.120f),  new Vector3(0.10f,  0.055f, 0.065f));
            // Nose bridge
            BuildCube  (h, "NoseBridge", skin,     new Vector3(0f,    0.090f,  0.010f),  new Vector3(0.035f, 0.055f, 0.080f));
            // Nose tip (Narcan landmark anchor)
            BuildSphere(h, "NoseTip",    skin,     new Vector3(0f,    0.120f,  0.050f),  0.025f);
            // Nostrils
            BuildSphere(h, "Nostril_L",  lips,     new Vector3(-0.022f, 0.090f, 0.052f), 0.013f);
            BuildSphere(h, "Nostril_R",  lips,     new Vector3( 0.022f, 0.090f, 0.052f), 0.013f);
            // Lips / mouth slit
            BuildCube  (h, "Mouth",      lips,     new Vector3(0f,    0.010f,  0.080f),  new Vector3(0.110f, 0.022f, 0.035f));
            BuildCube  (h, "UpperLip",   lips,     new Vector3(0f,    0.040f,  0.072f),  new Vector3(0.060f, 0.018f, 0.025f));
            // Ears
            BuildCube  (h, "Ear_L",      skin,     new Vector3(-0.132f, 0f, 0f),          new Vector3(0.030f, 0.065f, 0.052f));
            BuildCube  (h, "Ear_R",      skin,     new Vector3( 0.132f, 0f, 0f),          new Vector3(0.030f, 0.065f, 0.052f));
            // Closed eyelids
            BuildCube  (h, "EyeLid_L",   eyelid,   new Vector3(-0.055f,  0.090f, -0.020f), new Vector3(0.055f, 0.016f, 0.022f), Quaternion.Euler(5f, 0f, -14f));
            BuildCube  (h, "EyeLid_R",   eyelid,   new Vector3( 0.055f,  0.090f, -0.020f), new Vector3(0.055f, 0.016f, 0.022f), Quaternion.Euler(5f, 0f,  14f));
            // Brow ridges
            BuildCube  (h, "Brow_L",     skinDark, new Vector3(-0.055f,  0.106f, -0.038f), new Vector3(0.058f, 0.012f, 0.018f));
            BuildCube  (h, "Brow_R",     skinDark, new Vector3( 0.055f,  0.106f, -0.038f), new Vector3(0.058f, 0.012f, 0.018f));
            // Hair (crown / back of head)
            BuildCube  (h, "Hair",       hair,     new Vector3(0f,    0.038f, -0.028f),  new Vector3(0.22f,  0.060f, 0.220f));
        }

        static void BuildNeck(Transform root, Material skin)
        {
            BuildCube(root, "Neck", skin,
                new Vector3(0f, 0.13f, HeadLocalZ + 0.14f),
                new Vector3(0.10f, 0.09f, 0.11f));
        }

        /// <summary>
        /// Creates a "Torso" empty parent — the breathing-bob pivot — with UpperChest, LowerChest,
        /// Sternum, Collarbones and ribcage suggestion lines as children.
        /// </summary>
        static void BuildDetailedTorso(Transform root, Material shirt, Material ribMat, Material skinDark)
        {
            var torsoGO = new GameObject("Torso");
            torsoGO.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(torsoGO, "Torso");
            torsoGO.transform.localPosition = new Vector3(0f, 0.14f, TorsoLocalZ);
            var t = torsoGO.transform;

            // Chest blocks (local to Torso pivot)
            BuildCube(t, "UpperChest",   shirt,    new Vector3(0f,    0f,     -0.090f), new Vector3(0.42f, 0.22f, 0.320f));
            BuildCube(t, "LowerChest",   shirt,    new Vector3(0f,   -0.010f,  0.190f), new Vector3(0.38f, 0.18f, 0.260f));
            // Sternum (central bony ridge)
            BuildCube(t, "Sternum",      skinDark, new Vector3(0f,    0.110f,  0f),     new Vector3(0.030f, 0.012f, 0.280f));
            // Collarbones — angled outward from neck base toward shoulders
            BuildCube(t, "Collarbone_L", skinDark, new Vector3(-0.155f, 0.110f, -0.180f), new Vector3(0.20f, 0.022f, 0.032f), Quaternion.Euler(0f,  22f, 0f));
            BuildCube(t, "Collarbone_R", skinDark, new Vector3( 0.155f, 0.110f, -0.180f), new Vector3(0.20f, 0.022f, 0.032f), Quaternion.Euler(0f, -22f, 0f));
            // Ribcage suggestion lines (cosmetic, shirt-shade, 3 per side)
            BuildCube(t, "Rib_L1", ribMat, new Vector3(-0.155f, 0.110f, -0.050f), new Vector3(0.100f, 0.018f, 0.028f), Quaternion.Euler(0f, -8f, 0f));
            BuildCube(t, "Rib_L2", ribMat, new Vector3(-0.165f, 0.110f,  0.060f), new Vector3(0.090f, 0.018f, 0.028f));
            BuildCube(t, "Rib_L3", ribMat, new Vector3(-0.165f, 0.110f,  0.170f), new Vector3(0.080f, 0.018f, 0.028f), Quaternion.Euler(0f,  8f, 0f));
            BuildCube(t, "Rib_R1", ribMat, new Vector3( 0.155f, 0.110f, -0.050f), new Vector3(0.100f, 0.018f, 0.028f), Quaternion.Euler(0f,  8f, 0f));
            BuildCube(t, "Rib_R2", ribMat, new Vector3( 0.165f, 0.110f,  0.060f), new Vector3(0.090f, 0.018f, 0.028f));
            BuildCube(t, "Rib_R3", ribMat, new Vector3( 0.165f, 0.110f,  0.170f), new Vector3(0.080f, 0.018f, 0.028f), Quaternion.Euler(0f, -8f, 0f));
        }

        static void BuildPelvis(Transform root, Material pants, Material belt)
        {
            BuildCube(root, "HipCore",    pants, new Vector3(0f,      0.10f,  PelvisLocalZ),         new Vector3(0.40f, 0.18f, 0.22f));
            BuildCube(root, "HipCrest_L", pants, new Vector3(-0.225f, 0.20f,  PelvisLocalZ - 0.02f), new Vector3(0.07f, 0.04f, 0.14f));
            BuildCube(root, "HipCrest_R", pants, new Vector3( 0.225f, 0.20f,  PelvisLocalZ - 0.02f), new Vector3(0.07f, 0.04f, 0.14f));
            BuildCube(root, "Belt",       belt,  new Vector3(0f,      0.215f, PelvisLocalZ - 0.09f), new Vector3(0.42f, 0.028f, 0.040f));
        }

        static void BuildDetailedArm(Transform root, Material shirt, Material skin, Material skinDark,
            bool isLeft)
        {
            float s = isLeft ? -1f : 1f;
            string side = isLeft ? "Left" : "Right";
            char   sc   = isLeft ? 'L' : 'R';
            float ux = s * 0.28f;
            float fx = s * 0.30f;

            // Shoulder joint sphere (connects torso to upper arm — pip landmark anchor)
            BuildSphere(root, $"Shoulder_{sc}", skinDark,
                new Vector3(s * 0.24f, 0.14f, TorsoLocalZ - 0.20f), 0.060f);
            // Upper arm
            BuildCube  (root, $"UpperArm{side}", shirt,
                new Vector3(ux, 0.13f, TorsoLocalZ - 0.06f), new Vector3(0.10f, 0.12f, 0.36f));
            // Elbow joint sphere
            BuildSphere(root, $"Elbow_{sc}", skinDark,
                new Vector3(fx, 0.10f, TorsoLocalZ + 0.17f), 0.040f);
            // Forearm (skin visible below shirt sleeve)
            BuildCube  (root, $"Forearm{side}", skin,
                new Vector3(fx, 0.10f, TorsoLocalZ + 0.35f), new Vector3(0.08f, 0.10f, 0.30f));
            // Wrist joint sphere
            BuildSphere(root, $"Wrist_{sc}", skinDark,
                new Vector3(fx, 0.09f, TorsoLocalZ + 0.54f), 0.030f);
            // Back of hand
            BuildCube  (root, $"Hand{side}", skin,
                new Vector3(fx, 0.08f, TorsoLocalZ + 0.60f), new Vector3(0.085f, 0.038f, 0.100f));
            // Palm (lies slightly higher — face-up since patient is supine)
            BuildCube  (root, $"Palm_{sc}", skin,
                new Vector3(fx, 0.11f, TorsoLocalZ + 0.60f), new Vector3(0.090f, 0.018f, 0.100f));
            // Four fingers (spread outward from palm)
            for (int i = 0; i < 4; i++)
            {
                float fxf  = fx + s * (0.06f - i * 0.03f);
                float flen = 0.082f - i * 0.005f;
                BuildCube(root, $"Finger_{i + 1}{sc}", skin,
                    new Vector3(fxf, 0.10f, TorsoLocalZ + 0.68f), new Vector3(0.016f, 0.016f, flen));
            }
        }

        static void BuildDetailedLeg(Transform root, Material pants, Material skinDark, Material sock,
            Material shoe, bool isLeft)
        {
            float s = isLeft ? -1f : 1f;
            string side = isLeft ? "Left" : "Right";
            char   sc   = isLeft ? 'L' : 'R';
            float x = s * 0.10f;

            BuildCube  (root, $"Thigh{side}",  pants,    new Vector3(x, 0.12f, ThighLocalZ),          new Vector3(0.14f, 0.18f, 0.40f));
            BuildSphere(root, $"Knee_{sc}",    pants,    new Vector3(x, 0.10f, ThighLocalZ + 0.22f),  0.050f);
            BuildCube  (root, $"Shin{side}",   pants,    new Vector3(x, 0.10f, ShinLocalZ),            new Vector3(0.12f, 0.14f, 0.36f));
            // Ankle sphere (visible skin above shoe top)
            BuildSphere(root, $"Ankle_{sc}",   skinDark, new Vector3(x, 0.08f, ShinLocalZ + 0.22f),   0.040f);
            // Sock peek at the shoe opening
            BuildCube  (root, $"Sock_{sc}",    sock,     new Vector3(x, 0.11f, FootLocalZ - 0.06f),   new Vector3(0.11f, 0.090f, 0.050f));
            // Shoe
            BuildCube  (root, $"Shoe{side}",   shoe,     new Vector3(x, 0.08f, FootLocalZ),            new Vector3(0.12f, 0.10f, 0.20f));
            // Laces
            BuildCube  (root, $"Laces_{sc}",   sock,     new Vector3(x, 0.135f, FootLocalZ),           new Vector3(0.09f, 0.012f, 0.12f));
        }

        // ── HOTSPOT HELPERS ──────────────────────────────────────────────────

        /// <summary>
        /// Places a small visible pip sphere (<paramref name="pipRadius"/>) at <paramref name="localCenter"/>
        /// on the patient. Replaces the default SphereCollider with a generous BoxCollider
        /// (<paramref name="colliderSize"/> in world units) so ray selection stays comfortable on Quest,
        /// while the player visually sees the exact anatomical landmark.
        /// </summary>
        static void BuildHotspotWithPip(Transform parent, string name, Vector3 localCenter,
            float pipRadius, Vector3 colliderSize, Material pipMat,
            ScenarioHotspotId hotspotId, ScenarioRunner runner)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.localPosition = localCenter;
            float diameter = pipRadius * 2f;
            go.transform.localScale    = Vector3.one * diameter;
            ApplyMat(go, pipMat);

            // Replace the default SphereCollider with a generous BoxCollider.
            // BoxCollider.size is in GO local space; divide world-space extents by the uniform scale.
            var sc = go.GetComponent<SphereCollider>();
            if (sc != null) Object.DestroyImmediate(sc);

            var col = Undo.AddComponent<BoxCollider>(go);
            col.size     = colliderSize / diameter;
            col.isTrigger = false;

            var interactable = Undo.AddComponent<XRSimpleInteractable>(go);
            interactable.colliders.Clear();
            interactable.colliders.Add(col);

            var tag = Undo.AddComponent<RRXScenarioHotspotTag>(go);
            SetHotspotId(tag, hotspotId);

            var trigger = Undo.AddComponent<RRXTriggerActivatedHotspot>(go);
            trigger.SetRunner(runner);
            trigger.SetHotspotTag(tag);
            // Deactivates after correct use; re-enabled automatically on scenario reset.
            trigger.SetDisableAfterUse(true);

            // RRXHotspotHighlight auto-discovers the Renderer on this same GO (the pip sphere).
            Undo.AddComponent<RRXHotspotHighlight>(go);
        }

        static void BuildHotspot(Transform parent, string name, Vector3 localCenter, Vector3 localSize,
            Material material, ScenarioHotspotId hotspotId, ScenarioRunner runner, bool visible = true)
        {
            GameObject go;
            if (visible)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, name);
                go.transform.localPosition = localCenter;
                go.transform.localScale    = localSize;
                if (material != null)
                    ApplyMat(go, material);
                else
                    Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, name);
                go.transform.localPosition = localCenter;
                go.transform.localScale    = localSize;
                var col = Undo.AddComponent<BoxCollider>(go);
                col.isTrigger = false;

                var interactable = Undo.AddComponent<XRSimpleInteractable>(go);
                interactable.colliders.Clear();
                interactable.colliders.Add(col);

                var tag = Undo.AddComponent<RRXScenarioHotspotTag>(go);
                SetHotspotId(tag, hotspotId);
                var trigger = Undo.AddComponent<RRXTriggerActivatedHotspot>(go);
                trigger.SetRunner(runner);
                trigger.SetHotspotTag(tag);
                trigger.SetDisableAfterUse(true);
                Undo.AddComponent<RRXHotspotHighlight>(go);
                return;
            }

            var col2 = go.GetComponent<BoxCollider>();
            if (col2 != null) col2.isTrigger = false;

            var interactable2 = Undo.AddComponent<XRSimpleInteractable>(go);
            interactable2.colliders.Clear();
            if (col2 != null) interactable2.colliders.Add(col2);

            var tag2 = Undo.AddComponent<RRXScenarioHotspotTag>(go);
            SetHotspotId(tag2, hotspotId);
            var trigger2 = Undo.AddComponent<RRXTriggerActivatedHotspot>(go);
            trigger2.SetRunner(runner);
            trigger2.SetHotspotTag(tag2);
            Undo.AddComponent<RRXHotspotHighlight>(go);
        }

        static void BuildDecoys(Transform root, ScenarioRunner runner)
        {
            var pillMat = GetOrCreateMat("RRX_Mat_DecoyPill", new Color(0.85f, 0.72f, 0.12f, 0.75f));
            ApplyTransparent(pillMat);
            var syringeMat = GetOrCreateMat("RRX_Mat_DecoySyringe", new Color(0.88f, 0.92f, 0.95f, 0.65f));
            ApplyTransparent(syringeMat);
            var waterMat = GetOrCreateMat("RRX_Mat_DecoyWater", new Color(0.22f, 0.55f, 0.90f, 0.65f));
            ApplyTransparent(waterMat);

            BuildDecoy(root, "Decoy_PillBottle",
                localPos:   new Vector3(-0.55f, 0.06f, TorsoLocalZ + 0.15f),
                localScale: new Vector3(0.07f, 0.12f, 0.07f),
                mat: pillMat, label: "Pills", runner: runner);

            BuildDecoy(root, "Decoy_Syringe",
                localPos:   new Vector3(0.50f, 0.04f, TorsoLocalZ + 0.30f),
                localScale: new Vector3(0.04f, 0.04f, 0.18f),
                mat: syringeMat, label: "Syringe", runner: runner);

            BuildDecoy(root, "Decoy_WaterBottle",
                localPos:   new Vector3(-0.48f, 0.08f, PelvisLocalZ + 0.10f),
                localScale: new Vector3(0.07f, 0.15f, 0.07f),
                mat: waterMat, label: "Water", runner: runner);
        }

        static void BuildDecoy(Transform root, string name, Vector3 localPos, Vector3 localScale,
            Material mat, string label, ScenarioRunner runner)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            ApplyMat(go, mat);

            var col = go.GetComponent<CapsuleCollider>();
            if (col != null) col.isTrigger = false;

            var interactable = Undo.AddComponent<XRSimpleInteractable>(go);
            interactable.colliders.Clear();
            if (col != null) interactable.colliders.Add(col);

            var tag = Undo.AddComponent<RRXScenarioHotspotTag>(go);
            SetHotspotId(tag, ScenarioHotspotId.None);
            var trigger = Undo.AddComponent<RRXTriggerActivatedHotspot>(go);
            trigger.SetRunner(runner);
            trigger.SetHotspotTag(tag);
            trigger.SetDisableAfterUse(false);

            var labelGO = new GameObject("Label");
            Undo.RegisterCreatedObjectUndo(labelGO, "Label");
            labelGO.transform.SetParent(go.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var mesh = Undo.AddComponent<TextMesh>(labelGO);
            mesh.text           = label;
            mesh.anchor         = TextAnchor.MiddleCenter;
            mesh.characterSize  = 0.08f;
            mesh.fontSize       = 24;
            mesh.color          = new Color(1f, 0.8f, 0.2f);
        }

        static void BuildPhone(Transform root, Material phoneMat, ScenarioRunner runner)
        {
            var phone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            phone.name = "RRX_Patient_Phone";
            phone.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(phone, phone.name);
            phone.transform.localPosition = new Vector3(0.42f, 0.04f, 0.95f);
            phone.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
            phone.transform.localScale    = new Vector3(0.08f, 0.018f, 0.16f);
            ApplyMat(phone, phoneMat);

            var col = phone.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.isTrigger = false;
                col.size   = new Vector3(1.8f, 4.5f, 1.8f);
                col.center = new Vector3(0f, 0.9f, 0f);
            }

            var interactable = Undo.AddComponent<XRSimpleInteractable>(phone);
            interactable.colliders.Clear();
            if (col != null) interactable.colliders.Add(col);

            var tag = Undo.AddComponent<RRXScenarioHotspotTag>(phone);
            SetHotspotId(tag, ScenarioHotspotId.Phone);
            var trigger = Undo.AddComponent<RRXTriggerActivatedHotspot>(phone);
            trigger.SetRunner(runner);
            trigger.SetHotspotTag(tag);
            Undo.AddComponent<RRXHotspotHighlight>(phone);
            BuildPhoneLabel(phone.transform);
        }

        static void BuildPhoneLabel(Transform parent)
        {
            var label = new GameObject("RRX_PhoneLabel");
            Undo.RegisterCreatedObjectUndo(label, label.name);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            var mesh = Undo.AddComponent<TextMesh>(label);
            mesh.text          = "Call 911";
            mesh.anchor        = TextAnchor.MiddleCenter;
            mesh.characterSize = 0.05f;
            mesh.fontSize      = 36;
            mesh.color         = Color.white;
        }

        // ── PRIMITIVE BUILDERS ────────────────────────────────────────────────

        static GameObject BuildCube(Transform parent, string name, Material mat,
            Vector3 localPos, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = localScale;
            ApplyMat(go, mat);
            return go;
        }

        static GameObject BuildCube(Transform parent, string name, Material mat,
            Vector3 localPos, Vector3 localScale, Quaternion localRot)
        {
            var go = BuildCube(parent, name, mat, localPos, localScale);
            go.transform.localRotation = localRot;
            return go;
        }

        static GameObject BuildSphere(Transform parent, string name, Material mat,
            Vector3 localPos, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.localPosition = localPos;
            go.transform.localScale    = Vector3.one * (radius * 2f);
            ApplyMat(go, mat);
            return go;
        }

        // ── HELPERS ──────────────────────────────────────────────────────────

        static void WirePresenterToRunner(ScenarioRunner runner, PatientPresenter presenter)
        {
            if (runner == null || presenter == null) return;
            var so   = new SerializedObject(runner);
            var prop = so.FindProperty("_patient");
            if (prop == null) return;
            prop.objectReferenceValue = presenter;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(runner);
        }

        static void ApplyMat(GameObject go, Material mat)
        {
            if (mat == null) return;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        static Material GetOrCreateMat(string assetName, Color color)
        {
            var path = $"{MatFolder}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            if (mat.HasProperty("_Color"))     mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            EnsureMaterialFolder();
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// Creates the warm-emissive landmark pip material used by <see cref="BuildHotspotWithPip"/>.
        /// The emission ensures pips are readable under passthrough lighting without a dedicated scene light.
        /// </summary>
        static Material GetOrCreatePipMat()
        {
            const string assetName = "RRX_Mat_PatientLandmarkPip";
            var path     = $"{MatFolder}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat       = new Material(shader);
            var baseColor = new Color(1.0f, 0.92f, 0.75f);
            if (mat.HasProperty("_Color"))     mat.color = baseColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);

            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", new Color(0.40f, 0.30f, 0.14f));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            EnsureMaterialFolder();
            AssetDatabase.CreateAsset(mat, path);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void ApplyTransparent(Material mat)
        {
            if (mat == null) return;
            if (mat.HasProperty("_Mode"))   mat.SetFloat("_Mode", 3f);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))   mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
        }

        static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder(MatFolder))
            {
                var parent = "Assets/RRX";
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder("Assets", "RRX");
                AssetDatabase.CreateFolder(parent, "Materials");
            }
        }

        static void SetHotspotId(RRXScenarioHotspotTag tag, ScenarioHotspotId id)
        {
            var so = new SerializedObject(tag);
            var p  = so.FindProperty("_hotspotId");
            if (p == null) return;
            p.enumValueIndex = (int)id;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tag);
        }
    }
}
