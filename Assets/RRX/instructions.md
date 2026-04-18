# Rewind Rescue XR (RRX) — Build & run instructions

This document explains how anyone on the team can open the Unity project, generate the MR training scene **with one menu command**, optional tweaks, and how to build to **Meta Quest 3**.

---

## 1. What you need installed

| Requirement | Notes |
|-------------|--------|
| **Unity Editor** | **2022.3 LTS** (this repo targets **2022.3.62f3** — use the same major/minor if possible). Install via Unity Hub with modules: **Android Build Support** (SDK & NDK Tools, OpenJDK). |
| **Unity Hub** | Used to add/install the editor and Android modules. |
| **Meta Quest 3** | For on-device testing; enable **Developer Mode** and USB/wireless debugging per [Meta’s current docs](https://developer.meta.com/horizon/documentation/native/android/mobile-developer-mode/). |
| **USB cable or wireless ADB** | To install the built `.apk` / run from Unity **Build And Run**. |

---

## 2. First-time project open (before using RRX menus)

1. **Clone or copy** this repository and open the folder **`HackathonXR`** (the folder that contains **`Assets`**, **`Packages`**, **`ProjectSettings`**).
2. In **Unity Hub**, click **Add** / **Open** and select that project folder.
3. Unity may take several minutes to **import packages** and **compile scripts**. Wait until the progress bar finishes.
4. Open **Window → General → Console**.  
   - If you see **red errors**, fix them before continuing — **custom RRX menus only appear after scripts compile successfully**.
5. Optional sanity check: **Window → RRX → About RRX Tools**  
   - If a dialog appears, the **RRX** editor scripts loaded correctly.

---

## 3. One-click scene setup (recommended path)

RRX adds items to the top menu bar **`RRX`** and also under **`Window → RRX`** (same actions).

### Primary command — full MR demo in the **current** scene

1. Open or create any scene (e.g. **File → Open Scene** → `Assets/Scenes/SampleScene.unity`), or use your team’s scene.
2. Click:

   **`RRX → Build Complete MR Scene (Auto)`**

   or the same item under **`Window → RRX`**.

3. What this **single** command does, in order:
   - Ensures **XR Interaction Manager** and **XR Origin (XR Rig)** exist (from XR Interaction Toolkit).
   - Disables the extra **Main Camera** if it conflicts with the XR rig.
   - Creates **`RRX_Scenario`** with **Scenario Runner**, **Scenario Debug Hotkeys**, wires **patient**, **interaction zone**, **Phone** and **Narcan** props.
   - Generates **`RRX_Environment_Root`**: **circular plaza floor** (walkable disc), **segmented boundary ring**, **storefront / infrastructure** ring, ceiling, concourse props, zone markers, ambience **AudioSources** (assign clips under `Assets/RRX/Audio`; see §5).
   - Applies **MR camera hints** on **`XROrigin`** (transparent clear color for passthrough-friendly composition when supported).
4. **Save the scene**: **File → Save** (Ctrl/Cmd+S).

That is the **main “one click” workflow** — no manual placement of those objects is required if this succeeds.

### Alternative one-click — new scene file saved on disk

Use this if you want a dedicated scene asset instead of editing SampleScene:

1. Click **`RRX → New RRX_Overdose Scene And Setup`** (also under **`Window → RRX`**).
2. Unity creates **`Assets/RRX/Scenes/RRX_Overdose.unity`**, adds it to **Build Settings**, and runs the **same full pipeline** as **Build Complete MR Scene (Auto)**.
3. **Save** if prompted.

---

## 4. Optional step-by-step menus (same tools, split apart)

Use these if you only need part of the setup or you are debugging.

| Menu path | What it does |
|-----------|----------------|
| **`RRX → Setup Demo In Active Scene`** | Scenario + patient + zones + phone/Narcan only (no cube room, no MR camera hints). |
| **`RRX → Generate Public Plaza Blockout`** (or **Generate MR Cube Blockout**) | Only **`RRX_Environment_Root`**: public plaza blockout (clears previous children under that root). |
| **`RRX → Apply MR Camera Hints To XR Origin`** | Only sets XR cameras to solid clear with alpha 0 on **`XROrigin`**. |
| **`RRX → Quest Device Verify Checklist`** | Shows a short **Quest / MR** checklist dialog (does not build automatically). |

---

## 5. Play area size (circular plaza)

The walkable training floor is a **circle on XZ**, centered at the scene origin, controlled in code:

- File: [`Assets/RRX/Scripts/Core/RRXPlayArea.cs`](Scripts/Core/RRXPlayArea.cs)
- Constant: **`RRXPlayArea.RadiusMeters`** — radius of the **walkable disc** in meters (default **10** → **20 m** diameter plaza floor). Storefronts and ceiling extend **outside** this radius.
- Helper: **`RRXPlayArea.ContainsWalkableDiscXZ`** for runtime checks (locomotion / sensing).

After changing **`RadiusMeters`**, run **`RRX → Build Complete MR Scene (Auto)`** again (or at least **Generate Public Plaza Blockout**) so geometry and the scenario layout stay consistent.

---

## 6. Testing in the Unity Editor (without a headset)

1. Press **Play**.
2. With **Scenario Debug Hotkeys** on **`RRX_Scenario`**, use:
   - **1** — Check responsiveness  
   - **2** — Call 911  
   - **3** — Administer Narcan  
   - **R** — Rewind one checkpoint  

   If the project uses **Input System only** (Player Settings), these use the **new Input System** keyboard path in code.

**Note:** Passthrough / MR camera behavior is **limited or different** in Editor Play mode; treat device testing as authoritative for Quest.

---

## 7. Building for Meta Quest 3 (Android)

### 7.1 Player & XR settings (usually already set in repo)

- **File → Build Settings**
  - Select **Android**.
  - Click **Switch Platform** if Unity is not already on Android (first time can take a while).

- **Edit → Project Settings → Player**
  - **Company Name** / **Product Name** / **Package Name** — set something unique before store/sideload if needed.
  - **Minimum API Level** — must be compatible with Quest (project is configured for a recent minimum; adjust only if Meta docs require it).

- **Edit → Project Settings → XR Plug-in Management**
  - **Android** tab: **OpenXR** enabled.
  - **OpenXR** (Android): enable **Meta Quest** features and, for MR/passthrough, enable **Camera / passthrough-related** features required by **`com.unity.xr.meta-openxr`** per [Unity OpenXR Meta](https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@1.0/manual/index.html) for your package version.

### 7.2 Add your scene to the build

- **File → Build Settings → Scenes In Build**
  - Ensure the scene you prepared (e.g. **`Assets/RRX/Scenes/RRX_Overdose.unity`**) is listed and **checked**.

### 7.3 Build and install

1. Connect **Quest 3** (USB or authorized wireless debugging).
2. **File → Build Settings → Build And Run** (choose an output folder for the `.apk` when asked), **or** **Build** and install via **Side Quest** / **Meta Quest Developer Hub**, depending on your workflow.

---

## 8. Packages this project relies on (reference)

Declared in **`Packages/manifest.json`**, including:

- **`com.unity.xr.management`**, **`com.unity.xr.openxr`**
- **`com.unity.xr.interaction.toolkit`**
- **`com.unity.xr.meta-openxr`** (MR / Meta OpenXR integration — pulls related dependencies)

If Package Manager fails to resolve versions, open **Window → Package Manager**, note errors, and align **OpenXR** / **meta-openxr** versions per Unity’s compatibility notes.

---

## 9. Troubleshooting

| Problem | What to try |
|---------|-------------|
| No **`RRX`** menu at all | Wait for compile to finish; clear **Console** red errors; confirm **`Assets/RRX/Scripts/Editor/`** exists. Use **`Window → RRX`** as an alternate path. |
| **Build Complete MR Scene** errors on props | Delete old **`RRX_Prop_Phone`** / **`RRX_Prop_Narcan`** in Hierarchy and run the command again. |
| Passthrough black / wrong | Run **`RRX → Apply MR Camera Hints To XR Origin`**; enable Meta **Camera / passthrough** OpenXR features; confirm **`com.unity.xr.meta-openxr`** imported; test on **device**, not only Editor. |
| Android **input** warnings | Project uses **Input System** for Android; scenario hotkeys support new Input System when legacy input is off. |

---

## 10. Quick reference — where things live

| Path | Purpose |
|------|---------|
| [`Assets/RRX/Scripts/Core/`](Scripts/Core/) | Scenario logic, play area radius, patient presenter |
| [`Assets/RRX/Scripts/Editor/RRXDemoSceneWizard.cs`](Scripts/Editor/RRXDemoSceneWizard.cs) | **Build Complete MR Scene (Auto)** and related menus |
| [`Assets/RRX/Scripts/Editor/RRXCubeBlockoutMenu.cs`](Scripts/Editor/RRXCubeBlockoutMenu.cs) | Cube MR environment generation |
| [`Assets/RRX/Scripts/Runtime/RRXMrPresentationHints.cs`](Scripts/Runtime/RRXMrPresentationHints.cs) | MR-friendly camera clear |
| [`Assets/RRX/Scenes/`](Scenes/) | Optional **`RRX_Overdose.unity`** when created by menu |

---

**Summary:** Open the project in **Unity 2022.3**, wait for imports, click **`RRX → Build Complete MR Scene (Auto)`**, **save the scene**, then **Build Settings → Android → Build And Run** to Quest 3.
