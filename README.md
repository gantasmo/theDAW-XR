# GANTASMO MIDI

A standalone Meta Quest 3 app that turns the headset into a hands-only control and
capture surface for **theDAW**. Hand tracking becomes MIDI that drives the DAW, the
headset's passthrough view streams into the DAW's VJ engine as a live video source,
and several headsets can share one physical space for co-located performance.

Every link rides plain ADB over the USB-C cable, or a wireless ADB pairing. No Quest
Link tether and no Meta Quest Developer Hub (MQDH) casting are ever in the path, and
nothing on the desktop needs OBS or a manually launched scrcpy.

```
                 Quest 3  (standalone app, hands only)
  +-------------------------------------------------------------------+
  |  Hand-tracked MIDI surface + gestures   ->  QuestMidiSender       |
  |  Passthrough view + MR composite        ->  display / H.264       |
  |  Shared world frame + peer presence     ->  colocation netcode    |
  +---------------------------------+---------------------------------+
                                    |
                                    |  ADB over USB-C  (or wireless ADB)
                                    v
  +-------------------------------------------------------------------+
  |  theDAW  (desktop)                                                |
  |   MIDI   ->  global MIDI bus  ->  MIDI-learn  ->  DJ / VJ / MAKE  |
  |   video  ->  VJ camera source  (delinQuest / STITCH)             |
  +-------------------------------------------------------------------+
```

## What it does

Three integrations, each usable on its own:

1. **Hand tracking controls theDAW.** A floating 3D control surface (faders, knobs,
   buttons, and a crossfader) plus hand microgestures emit MIDI. theDAW receives it
   as an ordinary controller on its global MIDI bus, so any control maps to DJ, VJ,
   MAKE, or EDIT through MIDI-learn. See
   [the control surface README](Assets/QuestMidiBridge/Runtime/ControlSurface/README.md).
2. **Passthrough streaming into the VJ.** The headset's view reaches theDAW's VJ as a
   live video source over ADB, with no MQDH and no Quest Link. Two paths cover the
   whole headset display and the clean real-world composite. See
   [the passthrough README](Assets/GantasmoPassthrough/README.md).
3. **Easy co-located multiplayer.** A one-click setup wizard wires shared-frame
   alignment and lightweight peer presence so a room of headsets locks the surface
   and visuals to the same physical spot. See
   [the colocation README](Assets/GantasmoColocation/README.md).

## Modules

| Folder | What it is | Docs |
|---|---|---|
| `Assets/QuestMidiBridge/` | The MIDI send and return path: `QuestMidiSender`, microgesture source, and the Setup Wizard | [README](Assets/QuestMidiBridge/README.md) |
| `Assets/QuestMidiBridge/Runtime/ControlSurface/` | The floating 3D hand-tracked MIDI surface (faders, knobs, buttons, crossfader) built from an editable config preset, with an in-VR layout editor | [README](Assets/QuestMidiBridge/Runtime/ControlSurface/README.md) |
| `Assets/GantasmoPassthrough/` | Passthrough stitch plus the H.264 streamer that feeds theDAW's VJ as the **STITCH** source | [README](Assets/GantasmoPassthrough/README.md) |
| `Assets/GantasmoColocation/` | Co-located multiplayer: shared spatial frame and networked head and hand presence, with a setup wizard | [README](Assets/GantasmoColocation/README.md) |
| `Assets/QuestMidiBridge/Bridge~/` | An optional desktop Node bridge for any WebMIDI DAW (the `~` keeps it out of the APK) | [README](Assets/QuestMidiBridge/Bridge~/README.md) |

## Requirements

- **Unity 6.x** (this project: 6000.4.x, URP 17.4.0) on **Windows**.
- **Meta XR SDK** `com.meta.xr.sdk.all` 203.0.0 (Interaction SDK + Building Blocks).
- A **Quest 3** in Developer Mode, Horizon OS **v203**, with hand tracking on.
- **adb** (Android platform-tools) for the USB or wireless tunnel.
- **theDAW** running on the same desktop, with its `questmidi`, `questcast`, and
  `queststitch` backend modules enabled.
- Colocation adds **Netcode for GameObjects** 2.12 + **Unity Transport** 2.7.2 (the
  setup wizard installs the building blocks).
- The optional Node bridge adds **Node.js** and **loopMIDI** (only for a WebMIDI DAW
  other than theDAW).

## Quick start

1. Open the project in Unity and load `Assets/Scenes/QuestMIDI.unity`.
2. **Window > Quest MIDI Bridge > Setup Wizard** and work down the checklist
   (developer mode, ADB detection, bridge config). **Step 8** builds or repairs the
   XR MIDI surface and validates the scene wiring.
3. Enable **theDAW**'s `questmidi` backend module. With theDAW open, its MIDI input
   list shows the headset's controls on the global MIDI bus; map them with MIDI-learn.
4. Build and deploy the Android app to the Quest, or press **Play** in the Editor to
   test the MIDI chain on the desk first (see below).
5. For video, select **delinQuest** or **STITCH** in theDAW's VJ source list; the
   backend starts the relay it needs. See the
   [passthrough README](Assets/GantasmoPassthrough/README.md).
6. For a multi-headset set, run the colocation **Setup Wizard** on each headset. See
   the [colocation README](Assets/GantasmoColocation/README.md).

## How MIDI reaches theDAW

The simplest route uses no extra desktop software. `QuestMidiSender` frames each
message and sends it over a localhost TCP socket that `adb reverse` tunnels across the
cable. theDAW's `questmidi` backend module listens on that socket and republishes the
MIDI onto the browser's global MIDI bus, where it behaves like any hardware controller
and is mappable across DJ, VJ, MAKE, and EDIT.

The bridge is two-way: theDAW can send MIDI back to the headset over the same socket,
which app-side receivers can react to.

A second route exists for any WebMIDI DAW that is not theDAW: the desktop Node bridge
in `Bridge~/` relays the same TCP frames into a **loopMIDI** virtual port that the DAW
opens as an ordinary MIDI input. Use one route or the other, not both at once.

## Streaming, without MQDH or Quest Link

Two independent video paths reach theDAW's VJ engine, both over ADB:

- **delinQuest** mirrors the entire headset display (the mixed-reality scene plus the
  performer's MIDI surface) into the VJ. The `questcast` backend module runs a relay
  that speaks the scrcpy protocol to the headset and streams H.264 to the browser,
  decoded with WebCodecs.
- **STITCH** streams only the clean stitched passthrough, the real-world composite
  without the overlaid surface, as a separate source. `GantasmoStitchStreamer` encodes
  the stitch render texture with Android MediaCodec and sends it over an ADB-reversed
  socket to the `queststitch` backend bridge.

Neither path opens a Quest Link session or uses MQDH casting. The headset only needs
USB debugging enabled, or a wireless ADB pairing. Details:
[passthrough README](Assets/GantasmoPassthrough/README.md).

## Testing without the headset

Press **Play** in the Editor with theDAW running. `QuestMidiSender` connects to the
PC's own `127.0.0.1`, so the full MIDI path works on the desk. `adb reverse` only
matters once the app runs on the headset. The MediaCodec video encoder is Android-only,
so the streaming picture needs a real device, but the editor still exercises the relay
plumbing.

## Troubleshooting

**Sliders or knobs do not move or send MIDI (buttons work).**
The surface's sliders and knobs need a `HandGrabInteractable`. The Building Blocks
comprehensive rig ships hand-grab and poke interactors but no plain `GrabInteractor`,
so a bare `GrabInteractable` is never grabbed and the handle never moves. Run
**`GANTASMO > Repair XR MIDI Surface Interactions`** (or the wizard's *Repair
Interactions* button). Freshly built surfaces already include it. Details:
[ControlSurface README](Assets/QuestMidiBridge/Runtime/ControlSurface/README.md#sliderssknobs-do-nothing-repair-an-older-surface).

**No MIDI reaches the DAW.** Confirm `adb reverse` is set (the wizard has a one-click
button and theDAW's module sets it on start), the sender's TCP port matches, and the
`questmidi` module is enabled in theDAW.

**No video in the VJ.** Confirm the headset is listed by `adb devices`, USB debugging
is accepted on the headset, and the matching backend module (`questcast` for
delinQuest, `queststitch` for STITCH) is enabled. For STITCH, the scene must contain a
`GantasmoStitchStreamer`; the `GANTASMO > Add Passthrough Stitch To Scene` menu adds it.

## Project notes

- Source control is **Git** (`gantasmo/GANTASMO-MIDI`). A Unity Version Control
  (Plastic) workspace also lives alongside it; the `.plastic/` folder is gitignored.
- **Not a UPM package** (no asmdefs). It ships as a Unity project plus the optional
  desktop bridge.
- `Bridge~/node_modules/` is present for local runs. Exclude it from any release
  artifact; the bridge re-installs from `package.json`.
- App identity defaults (`DefaultCompany`, the `com.UnityTechnologies...urpblank`
  package name) must be set before a production build.
