using System;
using System.Collections.Generic;
using System.IO;
using RRX.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RRX.Editor
{
    /// <summary>
    /// MR training environment: white tiled circular plaza, 12-sided (dodecagon) three-story mall galleries with
    /// 5 m walkways and one store bay per edge per floor, plus railings, columns, and retail blockout props.
    /// </summary>
    static class RRXCubeBlockoutMenu
    {
        const string MatFolder = "Assets/RRX/Materials";

        const int DodecagonSides = 12;
        const int BoundarySegments = 12;

        const float BoundaryThickness = 0.18f;
        const float WalkwayDepthMeters = 5f;
        const float StoreDepthMeters = 2.85f;
        const float FloorToFloor = 3.5f;
        const int MallFloorCount = 3;
        const float DeckSlabY = 0.1f;
        const float StoreStoryHeight = 3.15f;
        const float CeilingY = MallFloorCount * FloorToFloor + StoreStoryHeight + 0.35f;
        const float TileStepMap = 0.62f;
        const float InnerGalleryMargin = 0.18f;
        const float UpperWalkwayPullInPerFloor = 1.12f;

        /// <summary>
        /// Inner mall apothem was originally <c>RRXPlayArea.RadiusMeters + boundary + margin</c> when the plaza
        /// disc was 10 m. Kept fixed so reducing <see cref="RRXPlayArea.RadiusMeters"/> only shrinks the central
        /// circle + boundary props, not the dodecagon storefronts.
        /// </summary>
        const float MallLayoutReferenceDiscRadius = 10f;

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

        [MenuItem("RRX/Strip Legacy Plaza Elevator From Active Scene", false, 42)]
        [MenuItem("Window/RRX/Strip Legacy Plaza Elevator From Active Scene", false, 42)]
        static void StripLegacyElevatorFromScene()
        {
            var found = new List<GameObject>();

            void Walk(Transform t)
            {
                if (t.name == "Plaza_ElevatorCore")
                    found.Add(t.gameObject);
                for (var i = 0; i < t.childCount; i++)
                    Walk(t.GetChild(i));
            }

            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                Walk(root.transform);

            foreach (var go in found)
                Undo.DestroyObjectImmediate(go);

            if (found.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[RRX] Removed {found.Count} Plaza_ElevatorCore object(s). Save the scene if you want this permanent.");
            }
            else
                Debug.Log("[RRX] No Plaza_ElevatorCore found in the active scene.");
        }

        /// <summary>Called by menu and <see cref="RRXDemoSceneWizard"/> full auto-build.</summary>
        public static void RunBlockoutGeneration()
        {
            EnsureMaterialFolder();
            float discR = RRXPlayArea.RadiusMeters;
            float t = BoundaryThickness;
            float aInner = MallLayoutReferenceDiscRadius + t + InnerGalleryMargin;
            float aWalkOuter = aInner + WalkwayDepthMeters;
            float aStoreOuter = aWalkOuter + StoreDepthMeters;

            var tileWhiteMat = GetOrCreateMatPlazaTileWhite();
            var tileTranslucentMat = GetOrCreateMatPlazaTileTranslucent();
            var boundaryMat = GetOrCreateMat("RRX_Mat_Boundary", new Color(0.28f, 0.3f, 0.34f));
            var interiorWallMat = GetOrCreateMat("RRX_Mat_Wall", new Color(0.52f, 0.54f, 0.58f));
            var accentMat = GetOrCreateMat("RRX_Mat_Accent", new Color(0.78f, 0.82f, 0.88f));
            var propMat = GetOrCreateMat("RRX_Mat_Prop", new Color(0.32f, 0.5f, 0.55f));
            var facadeMat = GetOrCreateMat("RRX_Mat_Facade", new Color(0.4f, 0.38f, 0.42f));
            var glassMat = GetOrCreateMat("RRX_Mat_StoreGlass", new Color(0.62f, 0.7f, 0.78f, 0.92f));
            ApplyGlassLike(glassMat);
            var darkTrimMat = GetOrCreateMat("RRX_Mat_TileBorder", new Color(0.22f, 0.2f, 0.19f));

            var root = GameObject.Find("RRX_Environment_Root");
            if (root == null)
            {
                root = new GameObject("RRX_Environment_Root");
                Undo.RegisterCreatedObjectUndo(root, "RRX Environment Root");
            }

            ClearEnvironmentChildren(root.transform);

            BuildFloorPhysicsDisc(root.transform, discR, t);
            BuildTiledMapFloor(root.transform, aStoreOuter, tileWhiteMat, darkTrimMat,
                RRXPlayArea.VirtualFloorHoleRadiusMeters);
            BuildInnerTranslucentFloor(root.transform, RRXPlayArea.VirtualFloorHoleRadiusMeters,
                tileTranslucentMat);
            BuildBoundaryRing(root.transform, discR, t, StoreStoryHeight * 0.92f, boundaryMat);
            BuildDodecagonMall(root.transform, aInner, aWalkOuter, aStoreOuter, glassMat, facadeMat, interiorWallMat,
                darkTrimMat);
            BuildOuterCurtainWall(root.transform, aStoreOuter, interiorWallMat);
            BuildRailingsAllFloors(root.transform, aInner, accentMat);
            BuildCornerColumns(root.transform, aStoreOuter, interiorWallMat);
            BuildStoreInteriors(root.transform, aInner, aWalkOuter, aStoreOuter, propMat, interiorWallMat, accentMat);
            BuildCeiling(root.transform, aStoreOuter, interiorWallMat);
            BuildZones(root.transform, discR);
            BuildAmbienceAudio(root.transform, discR);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(
                $"[RRX] Dodecagon mall generated with MR center domain radius {discR:0.##}m (translucent virtual tiles in center let ~70% passthrough bleed through). Save the scene.");
        }

        /// <summary>Edge bisector angle (XZ): midpoint of edge i lies at apothem * unit direction.</summary>
        static float EdgeBisectorRadians(int edgeIndex)
        {
            return (edgeIndex + 0.5f) * (2f * Mathf.PI / DodecagonSides);
        }

        static Vector3 EdgeOutwardXZ(int edgeIndex)
        {
            float phi = EdgeBisectorRadians(edgeIndex);
            return new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
        }

        static float EdgeLengthForApothem(float apothem)
        {
            return 2f * apothem * Mathf.Tan(Mathf.PI / DodecagonSides);
        }

        /// <summary>Inner gallery apothem; upper floors pull inward so walkways widen toward the atrium.</summary>
        static float InnerApothemForFloor(int floorIndex, float aInnerBase)
        {
            if (floorIndex <= 0)
                return aInnerBase;
            return Mathf.Max(aInnerBase - UpperWalkwayPullInPerFloor * floorIndex, aInnerBase * 0.72f);
        }

        static float MapBoundingRadius(float aStoreOuter)
        {
            return aStoreOuter / Mathf.Cos(Mathf.PI / DodecagonSides) + 1.1f;
        }

        static Vector3 InnerDodecagonVertex(int vertexIndex, float innerApothem)
        {
            float Rv = innerApothem / Mathf.Cos(Mathf.PI / DodecagonSides);
            float ang = vertexIndex * (2f * Mathf.PI / DodecagonSides);
            return new Vector3(Mathf.Cos(ang) * Rv, 0f, Mathf.Sin(ang) * Rv);
        }

        /// <summary>Radial slab along one dodecagon edge: width = edge chord, depth = outerApo - innerApo, height = heightY.</summary>
        static void PlaceEdgeRadialSlab(Transform parent, string objectName, int edgeIndex, float innerApothem,
            float outerApothem, float centerWorldY, float heightY, Material mat)
        {
            var dir = EdgeOutwardXZ(edgeIndex);
            float midA = (innerApothem + outerApothem) * 0.5f;
            float depth = outerApothem - innerApothem;
            float edgeLen = EdgeLengthForApothem(midA);

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(box, objectName);

            var center = dir * midA + Vector3.up * centerWorldY;
            box.transform.position = center;
            box.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            box.transform.localScale = new Vector3(edgeLen * 0.99f, heightY, depth);

            ApplyMat(box, mat);
        }

        static void BuildFloorPhysicsDisc(Transform parent, float playRadius, float thickness)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Plaza_FloorCollider";
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Plaza Floor Collider");

            const float defaultCylHeight = 2f;
            const float defaultCylRadius = 0.5f;
            const float slabY = 0.06f;
            float sy = slabY / defaultCylHeight;
            float sXZ = playRadius / defaultCylRadius;
            go.transform.localScale = new Vector3(sXZ, sy, sXZ);
            go.transform.localPosition = new Vector3(0f, -slabY * 0.5f, 0f);

            ReplacePrimitiveColliderWithMeshCollider(go);

            var r = go.GetComponent<MeshRenderer>();
            if (r != null)
            {
                Undo.DestroyObjectImmediate(r);
            }
        }

        static void BuildTiledMapFloor(Transform parent, float aStoreOuter, Material tileMat, Material borderMat,
            float centerHoleRadius)
        {
            var holder = new GameObject("Plaza_FloorTiles");
            holder.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(holder, "Plaza Floor Tiles");

            float extent = MapBoundingRadius(aStoreOuter);
            float r2 = extent * extent + 0.25f;
            float holeR2 = centerHoleRadius * centerHoleRadius;
            var count = 0;

            for (float x = -extent; x <= extent + 0.01f; x += TileStepMap)
            {
                for (float z = -extent; z <= extent + 0.01f; z += TileStepMap)
                {
                    if (x * x + z * z > r2)
                        continue;
                    if (x * x + z * z < holeR2)
                        continue;

                    var ix = Mathf.RoundToInt(x / TileStepMap);
                    var iz = Mathf.RoundToInt(z / TileStepMap);
                    var mat = (ix + iz) % 7 == 0 ? borderMat : tileMat;

                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Tile_{count:0000}";
                    tile.transform.SetParent(holder.transform, false);
                    Undo.RegisterCreatedObjectUndo(tile, tile.name);
                    tile.transform.localPosition = new Vector3(x, 0.02f, z);
                    tile.transform.localScale = new Vector3(TileStepMap * 0.98f, 0.035f, TileStepMap * 0.98f);
                    ApplyMat(tile, mat);
                    count++;
                }
            }
        }

        /// <summary>
        /// Fills the central MR hole (carved out by <see cref="BuildTiledMapFloor"/>) with semi-transparent
        /// tiles (alpha 0.3) so the inner domain leans toward the real room while still wearing a faint
        /// virtual tile grid on top of the Meta Quest AR camera passthrough.
        /// </summary>
        static void BuildInnerTranslucentFloor(Transform parent, float centerHoleRadius, Material translucentMat)
        {
            if (translucentMat == null || centerHoleRadius <= 0.01f)
                return;

            var holder = new GameObject("Plaza_FloorTiles_Inner");
            holder.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(holder, "Plaza Inner Translucent Tiles");

            float extent = centerHoleRadius + TileStepMap;
            float holeR2 = centerHoleRadius * centerHoleRadius;
            var count = 0;

            for (float x = -extent; x <= extent + 0.01f; x += TileStepMap)
            {
                for (float z = -extent; z <= extent + 0.01f; z += TileStepMap)
                {
                    if (x * x + z * z >= holeR2)
                        continue;

                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"TileMR_{count:0000}";
                    tile.transform.SetParent(holder.transform, false);
                    Undo.RegisterCreatedObjectUndo(tile, tile.name);
                    tile.transform.localPosition = new Vector3(x, 0.022f, z);
                    tile.transform.localScale = new Vector3(TileStepMap * 0.98f, 0.01f, TileStepMap * 0.98f);
                    ApplyMat(tile, translucentMat);
                    count++;
                }
            }
        }

        static void BuildBoundaryRing(Transform parent, float R, float thickness, float height, Material mat)
        {
            var ring = new GameObject("Plaza_BoundaryRing");
            ring.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(ring, "Plaza Boundary Ring");

            float midR = R + thickness * 0.5f;
            float segLen = 2f * midR * Mathf.Sin(Mathf.PI / DodecagonSides);

            for (var i = 0; i < BoundarySegments; i++)
            {
                float phi = EdgeBisectorRadians(i);
                var outward = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                float cx = outward.x * midR;
                float cz = outward.z * midR;

                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"Boundary_Seg_{i:00}";
                seg.transform.SetParent(ring.transform, false);
                Undo.RegisterCreatedObjectUndo(seg, seg.name);

                seg.transform.localPosition = new Vector3(cx, height * 0.5f, cz);
                seg.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
                seg.transform.localScale = new Vector3(segLen * 1.02f, height, thickness);

                ApplyMat(seg, mat);
            }
        }

        static void BuildDodecagonMall(Transform parent, float aInner, float aWalkOuter, float aStoreOuter,
            Material glassMat, Material facadeMat, Material deckMat, Material trimMat)
        {
            var mall = new GameObject("Plaza_DodecagonMall");
            mall.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(mall, "Plaza Dodecagon Mall");

            for (var f = 0; f < MallFloorCount; f++)
            {
                var floorRoot = new GameObject($"Mall_L{f}");
                floorRoot.transform.SetParent(mall.transform, false);
                Undo.RegisterCreatedObjectUndo(floorRoot, floorRoot.name);

                float yBase = f * FloorToFloor;
                float deckCenterY = yBase + DeckSlabY * 0.5f;
                float aInF = InnerApothemForFloor(f, aInner);

                for (var e = 0; e < DodecagonSides; e++)
                {
                    var deckMatPick = e % 3 == 0 ? trimMat : deckMat;
                    PlaceEdgeRadialSlab(floorRoot.transform, $"Walkway_L{f}_E{e:00}", e, aInF, aWalkOuter,
                        deckCenterY, DeckSlabY, deckMatPick);

                    float storeCenterY = yBase + DeckSlabY + StoreStoryHeight * 0.5f;
                    var storeMat = e % 2 == 0 ? glassMat : facadeMat;
                    BuildOpenFrontStoreShell(floorRoot.transform, f, e, aWalkOuter, aStoreOuter, storeCenterY,
                        StoreStoryHeight, storeMat);
                }
            }
        }

        /// <summary>U-shaped shell: back + side walls + floor only on outer half; opening at -local Z faces the atrium.</summary>
        static void BuildOpenFrontStoreShell(Transform parent, int floorIndex, int edgeIndex, float aWalkOuter,
            float aStoreOuter, float centerWorldY, float boxHeight, Material wallMat)
        {
            var dir = EdgeOutwardXZ(edgeIndex);
            float midA = (aWalkOuter + aStoreOuter) * 0.5f;
            float depth = aStoreOuter - aWalkOuter;
            float width = EdgeLengthForApothem(midA);
            const float t = 0.12f;
            float shellZ = depth * 0.32f;
            float shellDepth = depth * 0.58f;

            var root = new GameObject($"StoreOpen_L{floorIndex}_E{edgeIndex:00}");
            root.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(root, root.name);

            root.transform.position = dir * midA + Vector3.up * centerWorldY;
            root.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            void Wall(string n, Vector3 lp, Vector3 sc)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                w.name = n;
                w.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(w, n);
                w.transform.localPosition = lp;
                w.transform.localScale = sc;
                ApplyMat(w, wallMat);
            }

            Wall("Back", new Vector3(0f, 0f, depth * 0.5f - t * 0.5f),
                new Vector3(width * 0.96f, boxHeight, t));
            Wall("Left", new Vector3(-width * 0.5f + t * 0.5f, 0f, shellZ),
                new Vector3(t, boxHeight, shellDepth));
            Wall("Right", new Vector3(width * 0.5f - t * 0.5f, 0f, shellZ),
                new Vector3(t, boxHeight, shellDepth));
            Wall("Floor", new Vector3(0f, -boxHeight * 0.5f + t * 0.5f, shellZ),
                new Vector3(width * 0.92f, t, shellDepth));
        }

        static void BuildOuterCurtainWall(Transform parent, float aStoreOuter, Material wallMat)
        {
            var shell = new GameObject("Plaza_OuterShell");
            shell.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(shell, "Plaza Outer Shell");

            float extra = 0.35f;
            float aOut = aStoreOuter + extra;
            float totalH = MallFloorCount * FloorToFloor + 1.2f;
            float centerY = totalH * 0.5f;

            for (var e = 0; e < DodecagonSides; e++)
                PlaceEdgeRadialSlab(shell.transform, $"Curtain_E{e:00}", e, aStoreOuter, aOut, centerY, totalH,
                    wallMat);
        }

        static void BuildRailingsAllFloors(Transform parent, float aInner, Material railMat)
        {
            var rails = new GameObject("Plaza_Railings");
            rails.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(rails, "Plaza Railings");

            const float handrailThicknessZ = 0.11f;
            const float handrailHeightY = 0.1f;
            const float postEvery = 1.25f;

            for (var f = 0; f < MallFloorCount; f++)
            {
                float deckTopY = f * FloorToFloor + DeckSlabY;
                float handrailY = deckTopY + 1.02f;
                float aInF = InnerApothemForFloor(f, aInner);

                for (var v = 0; v < DodecagonSides; v++)
                {
                    var p0 = InnerDodecagonVertex(v, aInF);
                    var p1 = InnerDodecagonVertex((v + 1) % DodecagonSides, aInF);
                    var edgeVec = p1 - p0;
                    float edgeLen = edgeVec.magnitude;
                    var edgeDir = edgeVec / edgeLen;
                    var mid = (p0 + p1) * 0.5f + Vector3.up * handrailY;

                    var handrail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    handrail.name = $"HandrailRun_L{f}_V{v:00}";
                    handrail.transform.SetParent(rails.transform, false);
                    Undo.RegisterCreatedObjectUndo(handrail, handrail.name);
                    handrail.transform.position = mid;
                    handrail.transform.rotation = Quaternion.LookRotation(edgeDir, Vector3.up);
                    handrail.transform.localScale = new Vector3(handrailThicknessZ, handrailHeightY,
                        edgeLen * 1.02f);
                    ApplyMat(handrail, railMat);

                    float postHeight = handrailY - deckTopY - 0.04f;
                    float postCenterY = deckTopY + postHeight * 0.5f + 0.02f;
                    int nPosts = Mathf.Clamp(Mathf.RoundToInt(edgeLen / postEvery), 2, 12);
                    for (var p = 0; p <= nPosts; p++)
                    {
                        float t = p / (float)nPosts;
                        var xz = p0 + edgeDir * (edgeLen * t);
                        var postPos = new Vector3(xz.x, postCenterY, xz.z);
                        var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        post.name = $"RailPost_L{f}_V{v}_{p:00}";
                        post.transform.SetParent(rails.transform, false);
                        Undo.RegisterCreatedObjectUndo(post, post.name);
                        post.transform.position = postPos;
                        post.transform.localScale = new Vector3(0.07f, postHeight, 0.07f);
                        ApplyMat(post, railMat);
                    }
                }
            }
        }

        static void BuildCornerColumns(Transform parent, float aStoreOuter, Material structureMat)
        {
            var cols = new GameObject("Plaza_Columns");
            cols.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(cols, "Plaza Columns");

            float Rv = aStoreOuter / Mathf.Cos(Mathf.PI / DodecagonSides) + 0.15f;
            float colH = MallFloorCount * FloorToFloor + 0.4f;
            float yC = colH * 0.5f;

            for (var i = 0; i < DodecagonSides; i++)
            {
                float ang = i * (2f * Mathf.PI / DodecagonSides);
                var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                col.name = $"Column_V{i:00}";
                col.transform.SetParent(cols.transform, false);
                Undo.RegisterCreatedObjectUndo(col, col.name);
                col.transform.localPosition = new Vector3(Mathf.Cos(ang) * Rv, yC, Mathf.Sin(ang) * Rv);
                col.transform.localScale = new Vector3(0.65f, colH * 0.5f, 0.65f);
                ApplyMat(col, structureMat);
            }
        }

        static void BuildStoreInteriors(Transform parent, float aInner, float aWalkOuter, float aStoreOuter,
            Material propMat, Material shelfMat, Material accentMat)
        {
            var interiors = new GameObject("Plaza_StoreInteriors");
            interiors.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(interiors, "Plaza Store Interiors");

            float midStoreA = (aWalkOuter + aStoreOuter) * 0.5f;

            for (var f = 0; f < MallFloorCount; f++)
            {
                float yBase = f * FloorToFloor + DeckSlabY + 0.35f;

                for (var e = 0; e < DodecagonSides; e++)
                {
                    var dir = EdgeOutwardXZ(e);
                    float phi = EdgeBisectorRadians(e);
                    float tanHalf = Mathf.Tan(Mathf.PI / DodecagonSides);
                    float edgeHalf = midStoreA * tanHalf * 0.65f;
                    var tangent = new Vector3(-dir.z, 0f, dir.x);
                    int seed = e * 7919 + f * 10007;
                    var rng = new System.Random(seed);
                    var inwardBias = dir * 0.28f;

                    for (var k = 0; k < 3; k++)
                    {
                        float along = (k - 1) * (edgeHalf * 0.5f) + (float)(rng.NextDouble() - 0.5) * 0.28f;
                        var basePos = dir * (midStoreA + 0.22f) + inwardBias + tangent * along + Vector3.up * yBase;

                        BuildClothingRack(interiors.transform, $"Rack_L{f}_E{e}_{k}", basePos, phi, propMat, rng);
                    }

                    var shelfPos = dir * (midStoreA + 0.42f) + inwardBias * 0.5f + Vector3.up * (yBase + 0.85f);
                    BuildCube(interiors.transform, $"Shelf_L{f}_E{e}", shelfPos,
                        new Vector3(edgeHalf * 1.6f, 1.8f, 0.25f), Quaternion.Euler(0f, -phi * Mathf.Rad2Deg, 0f),
                        shelfMat);

                    if ((seed & 1) == 0)
                    {
                        var tablePos = dir * (midStoreA - 0.25f) + inwardBias * 0.35f + tangent * (edgeHalf * 0.28f) +
                                       Vector3.up * (yBase + 0.38f);
                        BuildCube(interiors.transform, $"Table_L{f}_E{e}", tablePos,
                            new Vector3(0.9f, 0.45f, 0.55f), Quaternion.Euler(0f, -phi * Mathf.Rad2Deg, 0f),
                            accentMat);
                    }

                    var manPos = dir * (midStoreA - 0.05f) + inwardBias * 0.4f + tangent * (-edgeHalf * 0.4f) +
                                 Vector3.up * (yBase + 0.52f);
                    BuildMannequin(interiors.transform, $"Mannequin_L{f}_E{e}", manPos, phi, propMat);
                }
            }
        }

        static void BuildClothingRack(Transform parent, string name, Vector3 pos, float yawRad, Material mat,
            System.Random rng)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, -yawRad * Mathf.Rad2Deg, 0f);
            Undo.RegisterCreatedObjectUndo(root, name);

            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "RackBar";
            bar.transform.SetParent(root.transform, false);
            Undo.RegisterCreatedObjectUndo(bar, bar.name);
            bar.transform.localPosition = new Vector3(0f, 0.55f, 0.04f);
            bar.transform.localScale = new Vector3(0.95f, 0.05f, 0.07f);
            ApplyMat(bar, mat);

            for (var s = -1; s <= 1; s += 2)
            {
                var peg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                peg.name = s < 0 ? "RackEnd_L" : "RackEnd_R";
                peg.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(peg, peg.name);
                peg.transform.localPosition = new Vector3(s * 0.46f, 0.55f, 0f);
                peg.transform.localScale = new Vector3(0.06f, 0.08f, 0.06f);
                ApplyMat(peg, mat);
            }
        }

        static void BuildMannequin(Transform parent, string name, Vector3 pos, float yawRad, Material mat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, -yawRad * Mathf.Rad2Deg, 0f);
            Undo.RegisterCreatedObjectUndo(root, name);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            cap.transform.SetParent(root.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            cap.transform.localScale = new Vector3(0.26f, 0.38f, 0.2f);
            ApplyMat(cap, mat);

            var plinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plinth.transform.SetParent(root.transform, false);
            plinth.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            plinth.transform.localScale = new Vector3(0.45f, 0.12f, 0.45f);
            ApplyMat(plinth, mat);
        }

        static void BuildPlazaProps(Transform parent, float R, Material propMat, Material accentMat,
            Material neutralMat)
        {
            var props = new GameObject("Plaza_Props");
            props.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(props, "Plaza Props");

            BuildCube(props.transform, "Prop_InfoKiosk", new Vector3(R * 0.32f, 1.15f, R * 0.38f),
                new Vector3(0.55f, 2.3f, 0.45f), Quaternion.identity, propMat);
            BuildCube(props.transform, "Prop_Directory", new Vector3(R * 0.22f, 1.55f, -R * 0.48f),
                new Vector3(0.08f, 1.9f, 1.1f), Quaternion.identity, accentMat);
            BuildCube(props.transform, "Prop_Bench", new Vector3(-R * 0.38f, 0.22f, R * 0.48f),
                new Vector3(1.8f, 0.44f, 0.55f), Quaternion.identity, neutralMat);
            BuildCube(props.transform, "Prop_Planter", new Vector3(-R * 0.55f, 0.32f, -R * 0.28f),
                new Vector3(1.1f, 0.64f, 1.1f), Quaternion.identity, propMat);
            BuildCube(props.transform, "Prop_Recycling", new Vector3(R * 0.52f, 0.38f, -R * 0.42f),
                new Vector3(0.5f, 0.76f, 0.5f), Quaternion.identity, neutralMat);
        }

        static void BuildPlazaExtraFurniture(Transform parent, float R, Material propMat, Material neutralMat)
        {
            var extra = new GameObject("Plaza_ConcourseFurniture");
            extra.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(extra, "Plaza Concourse Furniture");

            BuildCube(extra.transform, "SeatCluster_A", new Vector3(R * 0.22f, 0.2f, R * 0.62f),
                new Vector3(1.2f, 0.4f, 0.5f), Quaternion.identity, neutralMat);
            BuildCube(extra.transform, "SeatCluster_B", new Vector3(-R * 0.28f, 0.2f, -R * 0.55f),
                new Vector3(1f, 0.4f, 0.45f), Quaternion.Euler(0f, 35f, 0f), neutralMat);
            BuildCube(extra.transform, "LowTable_A", new Vector3(R * 0.48f, 0.28f, R * 0.12f),
                new Vector3(0.7f, 0.35f, 0.5f), Quaternion.identity, propMat);
            BuildCube(extra.transform, "LowTable_B", new Vector3(-R * 0.42f, 0.28f, R * 0.22f),
                new Vector3(0.55f, 0.32f, 0.55f), Quaternion.Euler(0f, -20f, 0f), propMat);
        }

        static void BuildCeiling(Transform parent, float aStoreOuter, Material ceilingMat)
        {
            float Rv = aStoreOuter / Mathf.Cos(Mathf.PI / DodecagonSides) + 3.2f;
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Plaza_Ceiling";
            ceiling.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(ceiling, "Plaza Ceiling");
            ceiling.transform.localPosition = new Vector3(0f, CeilingY, 0f);
            ceiling.transform.localScale = new Vector3(Rv * 2f, 0.14f, Rv * 2f);
            ApplyMat(ceiling, ceilingMat);
        }

        static void BuildHangingBanners(Transform parent, float playR, float aStoreOuter, Material propMat)
        {
            var banners = new GameObject("Plaza_Banners");
            banners.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(banners, "Plaza Banners");

            float y = CeilingY - 0.35f;
            float Rmid = (playR + aStoreOuter) * 0.5f;

            for (var i = 0; i < 6; i++)
            {
                float ang = i * (Mathf.PI / 3f) + 0.2f;
                var pos = new Vector3(Mathf.Cos(ang) * Rmid * 0.85f, y, Mathf.Sin(ang) * Rmid * 0.85f);
                BuildCube(banners.transform, $"Banner_{i}", pos, new Vector3(0.06f, 2.2f, 1.4f),
                    Quaternion.Euler(0f, -ang * Mathf.Rad2Deg + 90f, 0f), propMat);
            }
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
            mech.transform.localPosition = new Vector3(R * 0.75f, FloorToFloor * 1.5f, -R * 0.55f);
            Undo.RegisterCreatedObjectUndo(mech, "Ambience Mechanical");

            var srcMech = Undo.AddComponent<AudioSource>(mech);
            srcMech.loop = true;
            srcMech.playOnAwake = false;
            srcMech.spatialBlend = 1f;
            srcMech.minDistance = 2f;
            srcMech.maxDistance = 22f;
            srcMech.rolloffMode = AudioRolloffMode.Linear;
            srcMech.volume = 0.18f;

            // Optional ambience: clips can be assigned later under Assets/RRX/Audio — no warning by default.
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

        static Material GetOrCreateMatPlazaTileWhite()
        {
            const string assetName = "RRX_Mat_PlazaTileWhite";
            var path = $"{MatFolder}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var grout = CreateGroutTexture();
            var groutPath = $"{MatFolder}/RRX_Tex_GroutTile.png";
            File.WriteAllBytes(groutPath, grout.EncodeToPNG());
            AssetDatabase.ImportAsset(groutPath, ImportAssetOptions.ForceUpdate);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(groutPath);

            var shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Color"))
                mat.color = Color.white;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", 0.78f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.1f);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.82f);

            if (mat.HasProperty("_MainTex"))
                mat.SetTextureScale("_MainTex", new Vector2(4f, 4f));
            if (mat.HasProperty("_BaseMap"))
                mat.SetTextureScale("_BaseMap", new Vector2(4f, 4f));

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// Same grout tile look as the opaque plaza material but configured for alpha-blend transparency
        /// (alpha 0.3) so the Meta Quest AR camera passthrough dominates the floor while a subtle virtual
        /// tile grid remains visible on top.
        /// </summary>
        static Material GetOrCreateMatPlazaTileTranslucent()
        {
            const string assetName = "RRX_Mat_PlazaTileTranslucent";
            var path = $"{MatFolder}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                ApplyTileTranslucentSettings(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var groutPath = $"{MatFolder}/RRX_Tex_GroutTile.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(groutPath) == null)
            {
                var grout = CreateGroutTexture();
                File.WriteAllBytes(groutPath, grout.EncodeToPNG());
                AssetDatabase.ImportAsset(groutPath, ImportAssetOptions.ForceUpdate);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(groutPath);

            var shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tex);
                mat.SetTextureScale("_MainTex", new Vector2(4f, 4f));
            }
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", new Vector2(4f, 4f));
            }
            ApplyTileTranslucentSettings(mat);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// Configures a material for Standard/URP transparent rendering at alpha 0.3 (30% virtual tile,
        /// 70% passthrough bleed). Safe to call on an existing material to upgrade its flags.
        /// </summary>
        static void ApplyTileTranslucentSettings(Material mat)
        {
            if (mat == null)
                return;

            var color = new Color(1f, 1f, 1f, 0.3f);
            if (mat.HasProperty("_Color"))
                mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);

            if (mat.HasProperty("_Mode"))
                mat.SetFloat("_Mode", 3f);
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);

            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_SrcBlend"))
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))
                mat.SetInt("_ZWrite", 0);

            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", 0.5f);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.55f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.05f);
        }

        static Texture2D CreateGroutTexture()
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 4; x++)
                {
                    bool grout = (x + y) % 2 == 0;
                    var c = grout ? new Color(0.88f, 0.88f, 0.9f) : new Color(0.96f, 0.96f, 0.97f);
                    t.SetPixel(x, y, c);
                }
            }

            t.Apply();
            return t;
        }

        static void ApplyGlassLike(Material mat)
        {
            if (mat == null)
                return;
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", 0.92f);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.95f);
            if (mat.HasProperty("_Mode"))
                mat.SetFloat("_Mode", 3f);
        }

        static void BuildCube(Transform parent, string name, Vector3 localPos, Vector3 localScale, Quaternion rot,
            Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = localScale;
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
