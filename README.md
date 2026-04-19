# Rewind Rescue XR (RRX)

![Rewind Rescue XR](https://i.imgur.com/8DSqOpr.png)

Rewind Rescue XR is an immersive, rewindable mixed-reality emergency-response trainer built for rapid iteration and real-world training impact.

At a systems level, the experience starts by configuring an XR rig, scenario graph, world-space interface, and a physically grounded plaza environment. The near play area is represented as a low-opacity, comfort-aware interaction zone so users can stay aware of real-world boundaries and avoid collisions, while farther distances render a busy public background with moving pedestrians and layered ambience. That contrast is intentional: immediate safety in the user radius, realistic external pressure in the periphery.

This creates training conditions that simulate urgency without removing user control. Participants can practice decision-making, prioritize actions, hear escalating scene stress, and still rewind to previous checkpoints after mistakes to repeat steps under pressure until they become consistent.

## How UI and Tracking Work

The UI is headset-relative and user-tracked:
- A floating HUD is parented to the XR camera and rendered in world space, so interface position remains spatially stable relative to the user.
- HUD distance/height and crowd settings are adjustable in-app for different comfort/experience levels.
- A wrist objective panel tracks live scenario state, hints, mistakes, and time pressure cues.
- Split-panel visibility can be toggled by controller chord (`L2 + R2`) when no modal is open.

## Core Mechanics (What to Do)

Scenario flow is implemented as a strict 7-step state machine:
1. Scene safety scan
2. Check responsiveness
3. Open airway
4. Check breathing
5. Call for help
6. Administer Narcan
7. Recovery position

Mechanic highlights:
- Correct hotspots/actions advance state.
- Wrong sequencing is handled as failure escalation.
- Rewind checkpoints let users roll back and retry.
- Clock pressure and ambience escalation reinforce urgency.
- Patient breathing/visual condition are state-driven (including apnea states).

## Technical Documentation (Languages and Techniques)

- **Language:** C#
- **Engine:** Unity `2022.3.62f3`
- **Target:** Android / Meta Quest 3
- **XR stack:** OpenXR, XR Interaction Toolkit, Meta OpenXR integration
- **Architecture:**
  - `ScenarioRunner` finite-state control
  - snapshot-driven patient presentation (`PatientVisualState`)
  - runtime procedural animation + procedural audio synthesis
  - editor automation for one-click scene generation
  - world-space UI + XR input bindings
  - checkpoint rewind for deliberate practice loops

## Why This Helps Training

The software is built to convert bystander hesitation into repeatable action:
- immersive context improves realism and retention,
- rewindability reduces fear of failure and supports mastery learning,
- progressive pressure trains calm execution under stress,
- spatial interaction mirrors real response behavior better than flat simulations.

In short, it is designed to help turn bystanders into life-savers through immersive, repeatable, ethically grounded practice.

## Product Positioning and Evaluation Focus

This project is positioned for strong evaluation across social impact, technical execution, and user experience:
- **High social relevance:** overdose-response training with practical public-health value.
- **Responsible XR use:** safety-aware near-field design + stress simulation in controlled layers.
- **Fast deployment:** one-command scene setup, procedural assets, and quick iteration cycles.
- **Clear demo narrative:** problem, immersive intervention, measurable training loop (attempt -> fail -> rewind -> improve).

Recommended presentation emphasis:
1. Demonstrate real user flow in-headset within 2-3 minutes.
2. Show one mistake path and rewind recovery path.
3. Explain ethical/safety design choices in MR.
4. Tie features directly to learning outcomes and behavior change.
5. Show technical completeness (state machine, UI tracking, audio pressure, replayability).

## Project Specs

- Unity Editor: `2022.3.62f3`
- Target platform: Android (Meta Quest 3)
- XR stack:
  - OpenXR
  - XR Interaction Toolkit
  - Meta OpenXR integration

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

