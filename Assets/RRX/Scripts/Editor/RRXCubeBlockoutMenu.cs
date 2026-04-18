using RRX.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RRX.Editor
{
    /// <summary>
    /// MR training environment: circular plaza (walkable disc) surrounded by mall storefront / infrastructure blockout.
    /// </summary>
    static class RRXCubeBlockoutMenu
    {
        const string MatFolder = "Assets/RRX/Materials";

        const float CeilingY = 4f;
        const float FacadeHeight = 3.4f;
        const float BoundaryThickness = 0.18f;
        const int BoundarySegments = 24;
        const int StorefrontCount = 10;
        const float StoreRadialDepth = 2.4f;
        const float StoreGapDegrees = 8f;

        [MenuItem("RRX/Generate Public Plaza Blockout", false, 40)]
        [MenuItem("Window/RRX/Generate Public Plaza Blockout", false, 40)]
        static void GenerateBlockoutMenu()
        {
            RunBlockoutGeneration();
        }

        [MenuItem("RRX/Generate MR Cube Blockout", false, 41)]
        [MenuItem("Window/RRX/Generate MR Cube Blockout", false, 41)]
        static void GenerateBlockoutLegacyMenu()
        {
            RunBlockoutGeneration();
        }

        /// <summary>Called by menu and <see cref="RRXDemoSceneWizard"/> full auto-build.</summary>
        public static void RunBlockoutGeneration()
        {
            EnsureMaterialFolder();
            float R = RRXPlayArea.RadiusMeters;
            float t = BoundaryThickness;

            var floorMat = GetOrCreateMat("RRX_Mat_Floor", new Color(0.48f, 0.47f, 0.46f));
            var boundaryMat = GetOrCreateMat("RRX_Mat_Boundary", new Color(0.28f, 0.3f, 0.34f));
            var interiorWallMat = GetOrCreateMat("RRX_Mat_Wall", new Color(0.52f, 0.54f, 0.58f));
            var accentMat = GetOrCreateMat("RRX_Mat_Accent", new Color(0.78f, 0.82f, 0.88f));
            var propMat = GetOrCreateMat("RRX_Mat_Prop", new Color(0.32f, 0.5f, 0.55f));
            var facadeMat = GetOrCreateMat("RRX_Mat_Facade", new Color(0.4f, 0.38f, 0.42f));

            var root = GameObject.Find("RRX_Environment_Root");
            if (root == null)
            {
                root = new GameObject("RRX_Environment_Root");
                Undo.RegisterCreatedObjectUndo(root, "RRX Environment Root");
            }

            ClearEnvironmentChildren(root.transform);

            BuildCircularFloor(root.transform, R, floorMat);
            BuildBoundaryRing(root.transform, R, t, FacadeHeight, boundaryMat);
            BuildStorefrontRing(root.transform, R, t, facadeMat, interiorWallMat);
            BuildInfrastructure(root.transform, R, t, interiorWallMat, accentMat);
            BuildCeiling(root.transform, R, t, interiorWallMat);
            BuildPlazaProps(root.transform, R, propMat, accentMat, interiorWallMat);
            BuildZones(root.transform, R);
            BuildAmbienceAudio(root.transform, R);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(
                $"[RRX] Public plaza blockout: walkable disc radius {R} m, storefront ring + ceiling. Save the scene. Import clips under Assets/RRX/Audio and assign to Plaza_Audio children if needed.");
        }

        static void BuildCircularFloor(Transform parent, float radiusMeters, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Plaza_Floor";
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Plaza Floor");

            const float defaultCylHeight = 2f;
            const float defaultCylRadius = 0.5f;
            const float slabY = 0.05f;
            float sy = slabY / defaultCylHeight;
            float sXZ = radiusMeters / defaultCylRadius;
            go.transform.localScale = new Vector3(sXZ, sy, sXZ);
            go.transform.localPosition = new Vector3(0f, -slabY * 0.5f, 0f);

            ReplacePrimitiveColliderWithMeshCollider(go);

            ApplyMat(go, mat);
        }

        static void ReplacePrimitiveColliderWithMeshCollider(GameObject go)
        {
            var collider3D = go.GetComponent<Collider>();
            if (collider3D != null)
                Undo.DestroyObjectImmediate(collider3D);

            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                return;

            var mc = Undo.AddComponent<MeshCollider>(go);
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
        }

        static void BuildBoundaryRing(Transform parent, float R, float thickness, float height, Material mat)
        {
            var ring = new GameObject("Plaza_BoundaryRing");
            ring.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(ring, "Plaza Boundary Ring");

            float midR = R + thickness * 0.5f;
            float segArc = 2f * Mathf.PI * R / BoundarySegments * 1.04f;

            for (var i = 0; i < BoundarySegments; i++)
            {
                float theta = (i + 0.5f) * (2f * Mathf.PI / BoundarySegments);
                float cx = Mathf.Cos(theta) * midR;
                float cz = Mathf.Sin(theta) * midR;
                var outward = new Vector3(cx, 0f, cz).normalized;

                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"Boundary_Seg_{i:00}";
                seg.transform.SetParent(ring.transform, false);
                Undo.RegisterCreatedObjectUndo(seg, seg.name);

                seg.transform.localPosition = new Vector3(cx, height * 0.5f, cz);
                seg.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
                seg.transform.localScale = new Vector3(segArc, height, thickness);

                ApplyMat(seg, mat);
            }
        }

        static void BuildStorefrontRing(Transform parent, float R, float t, Material facadeMat, Material trimMat)
        {
            var stores = new GameObject("Plaza_Storefronts");
            stores.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(stores, "Plaza Storefronts");

            float innerFace = R + t + 0.15f;
            float outerFace = innerFace + StoreRadialDepth;
            float midR = (innerFace + outerFace) * 0.5f;
            float sector = 360f / StorefrontCount;

            for (var i = 0; i < StorefrontCount; i++)
            {
                float angle0 = i * sector + StoreGapDegrees * 0.5f;
                float angle1 = (i + 1) * sector - StoreGapDegrees * 0.5f;
                float delta = Mathf.Max(angle1 - angle0, 4f);
                float thetaMid = (angle0 + angle1) * 0.5f * Mathf.Deg2Rad;
                float arcWidth = 2f * midR * Mathf.Sin(delta * 0.5f * Mathf.Deg2Rad);

                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Storefront_{i:00}";
                box.transform.SetParent(stores.transform, false);
                Undo.RegisterCreatedObjectUndo(box, box.name);

                float cx = Mathf.Cos(thetaMid) * midR;
                float cz = Mathf.Sin(thetaMid) * midR;
                var outMid = new Vector3(cx, 0f, cz).normalized;

                box.transform.localPosition = new Vector3(cx, FacadeHeight * 0.5f, cz);
                box.transform.localRotation = Quaternion.LookRotation(outMid, Vector3.up);
                box.transform.localScale = new Vector3(arcWidth * 0.98f, FacadeHeight, StoreRadialDepth);

                ApplyMat(box, i % 2 == 0 ? facadeMat : trimMat);
            }
        }

        static void BuildInfrastructure(Transform parent, float R, float t, Material structureMat, Material accentMat)
        {
            var infra = new GameObject("Plaza_Infrastructure");
            infra.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(infra, "Plaza Infrastructure");

            float colR = R + t + 0.35f;
            for (var k = 0; k < 4; k++)
            {
                float ang = (k * 90f + 22f) * Mathf.Deg2Rad;
                var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                col.name = $"Column_{k}";
                col.transform.SetParent(infra.transform, false);
                Undo.RegisterCreatedObjectUndo(col, col.name);
                col.transform.localPosition = new Vector3(Mathf.Cos(ang) * colR, 2f, Mathf.Sin(ang) * colR);
                col.transform.localScale = new Vector3(0.55f, 2f, 0.55f);
                ApplyMat(col, structureMat);
            }

            float escTheta = 205f * Mathf.Deg2Rad;
            float escR = R + t + StoreRadialDepth * 0.5f + 0.4f;
            var esc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            esc.name = "Prop_EscalatorRead";
            esc.transform.SetParent(infra.transform, false);
            Undo.RegisterCreatedObjectUndo(esc, esc.name);
            esc.transform.localPosition = new Vector3(Mathf.Cos(escTheta) * escR, 1.1f, Mathf.Sin(escTheta) * escR);
            esc.transform.localRotation = Quaternion.Euler(12f, -escTheta * Mathf.Rad2Deg + 90f, 0f);
            esc.transform.localScale = new Vector3(1.8f, 2.2f, 3.2f);
            ApplyMat(esc, accentMat);
        }

        static void BuildCeiling(Transform parent, float R, float t, Material ceilingMat)
        {
            float extent = R + t + StoreRadialDepth + 2f;
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Plaza_Ceiling";
            ceiling.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(ceiling, "Plaza Ceiling");
            ceiling.transform.localPosition = new Vector3(0f, CeilingY, 0f);
            ceiling.transform.localScale = new Vector3(extent * 2f, 0.12f, extent * 2f);
            ApplyMat(ceiling, ceilingMat);
        }

        static void BuildPlazaProps(Transform parent, float R, Material propMat, Material accentMat,
            Material neutralMat)
        {
            var props = new GameObject("Plaza_Props");
            props.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(props, "Plaza Props");

            BuildCube(props.transform, "Prop_InfoKiosk", new Vector3(R * 0.32f, 1.15f, R * 0.38f),
                new Vector3(0.55f, 2.3f, 0.45f), propMat);
            BuildCube(props.transform, "Prop_Directory", new Vector3(R * 0.22f, 1.55f, -R * 0.48f),
                new Vector3(0.08f, 1.9f, 1.1f), accentMat);
            BuildCube(props.transform, "Prop_Bench", new Vector3(-R * 0.38f, 0.22f, R * 0.48f),
                new Vector3(1.8f, 0.44f, 0.55f), neutralMat);
            BuildCube(props.transform, "Prop_Planter", new Vector3(-R * 0.55f, 0.32f, -R * 0.28f),
                new Vector3(1.1f, 0.64f, 1.1f), propMat);
            BuildCube(props.transform, "Prop_Recycling", new Vector3(R * 0.52f, 0.38f, -R * 0.42f),
                new Vector3(0.5f, 0.76f, 0.5f), neutralMat);
        }

        static void BuildZones(Transform parent, float R)
        {
            var zones = new GameObject("Plaza_Zones");
            zones.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(zones, "Plaza Zones");

            CreateZoneEmpty(zones.transform, "Zone_Entry", new Vector3(0f, 0f, -R * 0.82f));
            CreateZoneEmpty(zones.transform, "Zone_Observation", Vector3.zero);
            CreateZoneEmpty(zones.transform, "Zone_Response", new Vector3(-R * 0.52f, 0f, R * 0.52f));
            CreateZoneEmpty(zones.transform, "Zone_Peripheral", new Vector3(R * 0.68f, 0f, -R * 0.32f));
            CreateZoneEmpty(zones.transform, "Zone_Boundary", new Vector3(R * 0.95f, 0f, 0f));
            CreateZoneEmpty(zones.transform, "Spawn_Focal", new Vector3(0f, 0f, R * 0.5f));
            CreateZoneEmpty(zones.transform, "LoS_Check_A", new Vector3(-R * 0.58f, 1.65f, -R * 0.62f));
            CreateZoneEmpty(zones.transform, "LoS_Check_B", new Vector3(R * 0.52f, 1.65f, -R * 0.48f));
        }

        static void CreateZoneEmpty(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Undo.RegisterCreatedObjectUndo(go, name);
        }

        static void BuildAmbienceAudio(Transform parent, float R)
        {
            var audioRoot = new GameObject("Plaza_Audio");
            audioRoot.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(audioRoot, "Plaza Audio");

            var bed = new GameObject("Ambience_MallBed");
            bed.transform.SetParent(audioRoot.transform, false);
            bed.transform.localPosition = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(bed, "Ambience Mall Bed");

            var srcBed = Undo.AddComponent<AudioSource>(bed);
            srcBed.loop = true;
            srcBed.playOnAwake = false;
            srcBed.spatialBlend = 0f;
            srcBed.volume = 0.22f;

            var mech = new GameObject("Ambience_Mechanical");
            mech.transform.SetParent(audioRoot.transform, false);
            mech.transform.localPosition = new Vector3(R * 0.75f, 2.2f, -R * 0.55f);
            Undo.RegisterCreatedObjectUndo(mech, "Ambience Mechanical");

            var srcMech = Undo.AddComponent<AudioSource>(mech);
            srcMech.loop = true;
            srcMech.playOnAwake = false;
            srcMech.spatialBlend = 1f;
            srcMech.minDistance = 2f;
            srcMech.maxDistance = 18f;
            srcMech.rolloffMode = AudioRolloffMode.Linear;
            srcMech.volume = 0.18f;

            if (srcBed.clip == null || srcMech.clip == null)
                Debug.LogWarning(
                    "[RRX] Plaza ambience AudioSources have no clips. Add mall / HVAC loops under Assets/RRX/Audio and assign on Plaza_Audio.");
        }

        static void ClearEnvironmentChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                Undo.DestroyObjectImmediate(child);
            }
        }

        static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/RRX"))
                AssetDatabase.CreateFolder("Assets", "RRX");
            if (!AssetDatabase.IsValidFolder(MatFolder))
                AssetDatabase.CreateFolder("Assets/RRX", "Materials");
        }

        static Material GetOrCreateMat(string assetName, Color color)
        {
            var path = $"{MatFolder}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            if (mat.HasProperty("_Color"))
                mat.color = color;
            else if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void BuildCube(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            ApplyMat(go, mat);
        }

        static void ApplyMat(GameObject go, Material mat)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r != null && mat != null)
            {
                Undo.RecordObject(r, "RRX Blockout mat");
                r.sharedMaterial = mat;
            }
        }
    }
}
