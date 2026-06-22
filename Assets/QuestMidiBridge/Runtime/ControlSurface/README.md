# GANTASMO XR MIDI Control Surface

A 3D, hand-tracked control surface that turns XR interactions into MIDI and sends
them to theDAW through the Quest MIDI bridge. The default is a curved DJ fan:
**6 faders on an upward arc, a horizontal crossfade, 8 knobs on an arc, and 12
buttons (a tilted upper row over a flat lower row)**, floating with no backboard.
Faders and knobs carry a `HandGrabInteractable` (hand + controller hand-grab);
buttons carry a `PokeInteractable`. Those are the interactables the Meta Building
Blocks rig actually drives, so the hands move them with no per-control wiring. A
hamburger button opens an in-VR editor for moving, scaling, and rotating the whole
surface (see below).

## Build it

Run `GANTASMO ▸ Build XR MIDI Control Surface`. The builder reads a **config preset**
(see below), creates a `GANTASMO XR MIDI Surface` GameObject in the open scene, lays
out the controls, wires them to a single `QuestMidiSender` through a
`MidiControlSurface`, and saves the prefab to `Assets/QuestMidiBridge/Prefabs/`. The
root floats at `(0, 1.1, 0.35)` by default.

## Config presets — arrange it your way

The layout is data, not code. The builder reads a `GantasmoSurfaceConfig` asset
(auto-created at `Assets/QuestMidiBridge/Config/GantasmoSurfaceConfig.asset` on first
build). Select it and edit, per control group (sliders / knobs / buttons):

- **count**, **columns** (rows stack downward), and **spacing**
- **origin** (where the group sits within the root) and the root **position / rotation**
- **scale** of each control
- **material** for the grabbable/pressable part
- **customPrefab** — your own 3D object used instead of the generated cube; it gets the
  interactable + MIDI components and a collider added automatically
- **positionOverrides** — explicit per-control positions that beat the grid

Menu actions:

| Menu | Does |
|---|---|
| `GANTASMO ▸ Build XR MIDI Control Surface` | Build from the default config |
| `GANTASMO ▸ Reset Surface Config To Default Layout` | Rewrite the default preset to the curved DJ fan (run this once if an older grid config already exists, then rebuild) |
| `GANTASMO ▸ Create Surface Config Preset` | Make a new preset asset to edit (duplicate/tweak) |
| `GANTASMO ▸ Build Surface From Selected Config` | Build from the preset selected in the Project window |
| `GANTASMO ▸ Capture Surface Layout Into Selected Config` | Save a surface you nudged by hand back into a preset (fills `positionOverrides`, counts, root transform) |

So: arrange by editing the preset and rebuilding, or move controls in the Scene view
and **Capture** them back into a preset. Clear `positionOverrides` to return to the grid.

## Edit the layout in VR

A hamburger button sits at the top-right of the surface. Poke it to enter edit mode:
the controls stop sending MIDI and a green grab bar appears at the top. Grab the bar
with one hand to move the whole surface (drag it up for height); grab it with both
hands to rotate and scale. Poke **Save** to write the placement as this scene's
default, or **Done** to leave without saving. The saved placement is reapplied on
the next launch.

Saved layouts live in `Application.persistentDataPath`, keyed by scene and surface
name, so the Editor and the headset each keep their own and several scenes stay
independent. `SurfaceLayoutStore.Clear` (or deleting the JSON) returns the surface to
its built placement. Per-control nudging is authored in the Editor with **Capture
Surface Layout** (above); the in-VR editor moves the surface as a whole.

## The hand-tracking rig

The controls are interactables; the hands provide the interactors. The surface is
built for the Meta Building Blocks **comprehensive interaction rig**, which ships
hand-grab and poke interactors (this is exactly what `Scenes/QuestMIDI.unity`
uses — `[BuildingBlock] OVRComprehensiveInteractionRig`).

> **Why hand-grab, not plain Grab.** The comprehensive rig contains
> `HandGrabInteractor` / `DistanceHandGrabInteractor` / `TouchControllerHandGrabInteractor`
> and a `PokeInteractor` — but **no plain `GrabInteractor`**. A bare
> `GrabInteractable` is therefore never selected by any interactor, the handle
> never moves, and no MIDI is sent. The controls must carry a `HandGrabInteractable`
> for hands to grab them. The builder adds this automatically.

Grab sliders and knobs with a pinch or palm grab; press buttons with a fingertip.

### Sliders/knobs do nothing? (repair an older surface)

If you built or imported a surface before this fix, its sliders/knobs may only have
a plain `GrabInteractable` and won't respond. Run **`GANTASMO ▸ Repair XR MIDI
Surface Interactions`** (or the **Repair Interactions** button in the Setup Wizard).
It adds the missing `HandGrabInteractable` to every slider/knob in the surface
prefab — and any non-prefab surface in the open scene — without a full rebuild, so
CC assignments and placement are preserved. Poke buttons are unaffected; they work
because the rig already has a matching poke interactor.

## What each control sends (channel 1)

| Control | Count (default) | Interaction | MIDI (default) |
|---|---|---|---|
| Fader | 6 | grab + slide on its rail | CC 1-6, value from travel |
| Crossfade | 1 | grab + slide horizontally | CC 7, value from travel |
| Knob | 8 | grab + twist, ±135° | CC 40-47, value from angle |
| Button | 12 | poke (push) | Note 36-47, on while pressed |

Counts and CC/Note start numbers come from the config preset, so these ranges shift
if you change them.

Sliders and knobs send 7-bit CC by default. `MidiControlSurface.highResolution`
switches to 14-bit, which consumes both `cc` and `cc+32`; re-allocate the CC
numbers into the 0-31 range before enabling it across the whole surface.

`MidiButton.mode` also offers `ToggleCC`, which latches a CC between 0 and 127 on
each press instead of sending notes.

## Hand gestures (alongside the controls)

Hands can also drive theDAW without touching a control. `MicrogestureMidiSource`
(one component per hand) wraps the Quest microgesture recognizer and emits a momentary
MIDI message on each recognized gesture. The defaults map Notes 48-52 to swipe left,
swipe right, swipe forward, swipe backward, and thumb tap; set `emitAsControlChange` to
send a 127-then-0 CC pulse instead. Each gesture is debounced and lands on the same
`MidiControlSurface` channel, so it appears in theDAW's MIDI-learn next to the surface
controls. Hand-pose recognition to MIDI is on the roadmap; microgestures and the
grab/poke controls are what ship today.

## How it reaches theDAW

`QuestMidiSender` frames each message and sends it over the localhost TCP socket
that the desktop bridges with `adb reverse`. theDAW's `questmidi` backend module
relays it onto the global `midiBus`, where it behaves like any hardware controller
and can be mapped to DJ, VJ, and MAKE controls with MIDI-learn.

## Tuning

- `MidiSlider.minLocal` / `maxLocal` set the rail endpoints that map to 0 and 1.
- `MidiKnob.minAngle` / `maxAngle` set the twist range; the rest pose reads as the
  centre, so a symmetric range starts at 0.5.
- Slider rail throw, knob angle range, grid spacing, scale, and CC/Note numbering all
  live in the `GantasmoSurfaceConfig` preset — edit the asset and rebuild.
