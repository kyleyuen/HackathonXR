# HackathonXR - Rewind Rescue XR (RRX)

Unity XR mixed-reality training prototype for overdose-response simulation, including:
- 7-step scenario state machine
- patient visual progression (including apnea handling)
- procedural audio feedback and ambience
- world-space XR HUD and wrist objective panel

## Project Specs

- Unity Editor: `2022.3.62f3`
- Target platform: Android (Meta Quest 3)
- XR stack:
  - OpenXR
  - XR Interaction Toolkit
  - Meta OpenXR integration
- Main scenario flow:
  1. Scene safety scan
  2. Check responsiveness
  3. Open airway
  4. Check breathing
  5. Call for help
  6. Administer Narcan
  7. Recovery position

## Key Gameplay/Systems

- **Breathing mechanics**
  - Breathing is driven by `PatientVisualState` snapshots from `ScenarioRunner`.
  - `IsApnea` + normalized `BreathRate` are used for realistic no-breath states.
  - Procedural torso bob and patient breath audio are both state-driven.

- **Scenario correctness**
  - Runner-based action/hotspot validation.
  - Rewind checkpoints include clock state restoration.
  - Debug hotkeys cover all scenario steps.

- **UI/UX**
  - Floating HUD with settings, scenario panel, training panel.
  - Wrist panel objective + hint system and clock warning tinting.
  - Main menu includes L2+R2 hint for split panel toggle.

- **Audio**
  - Procedural SFX bank (`ok`, `bad`, `recovered`, ambience beds, breathing loops).
  - Recovery cue no longer double-plays with generic success cue.
  - Crowd loop deduplication when emergency ambience system is present.
  - Short ambience ducking on feedback events.

## Setup Instructions

1. Open this folder in Unity Hub (`HackathonXR` root).
2. Use Unity `2022.3.62f3` with Android Build Support modules installed.
3. Let package import and script compilation complete.
4. If Console has errors, resolve them before running RRX menu tools.

## Build Scene (Recommended)

From Unity menu:

- `RRX -> Build Complete MR Scene (Auto)`

This command wires XR rig dependencies, scenario graph, environment blockout, patient/hotspots, HUD, feedback, ambience, and mixer wiring support.

## Run in Editor

- Press Play.
- Use debug hotkeys:
  - `1` Scene safety scan
  - `2` Check responsiveness
  - `3` Open airway
  - `4` Check breathing
  - `5` Call 911
  - `6` Administer Narcan
  - `7` Recovery position
  - `R` Rewind previous checkpoint

## Build to Quest 3

1. `File -> Build Settings` and switch to Android.
2. Ensure target scene is in **Scenes In Build**.
3. Confirm OpenXR + Meta features are enabled for Android.
4. Use **Build And Run** with Quest connected.

## Controls / Interaction Notes

- Wrist and world interactions use XR ray + trigger (`UI Press` mappings).
- HUD:
  - `Start` opens side panels
  - `Settings` controls HUD placement and crowd settings
  - `Scenario` displays live state and allows reset
  - `L2 + R2` toggles split panels when no modal overlay is open

## Important Paths

- `Assets/RRX/Scripts/Core/` - scenario state machine and data models
- `Assets/RRX/Scripts/Runtime/` - runtime visuals, audio, HUD, interaction behavior
- `Assets/RRX/Scripts/Editor/` - one-click scene builders and setup menus
- `Assets/RRX/instructions.md` - detailed team-facing setup/build walkthrough

## Troubleshooting

- No `RRX` menu:
  - wait for compile/import to finish
  - check Console for script errors
- No sound:
  - verify `RRX_Feedback` and `RRX_Ambience` objects exist in scene
  - verify Audio Listener exists on XR camera
- Scenario buttons fail:
  - ensure `ScenarioRunner` exists and hotspots were built/wired by wizard

