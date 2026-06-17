# GANTASMO MIDI

A **standalone Meta Quest 3 app** that turns hand-tracked XR interactions into MIDI
and ships it to a desktop **WebMIDI DAW** (theDAW) over the **USB-C cable only** — no
Wi-Fi, no network on stage. A return circuit lets the DAW drive a head-mounted,
MIDI-reactive visor back on the headset.

The headset runs everything; the desktop just receives the MIDI signal (and, on the
roadmap, the headset's video — see [Video feed](#video-feed-investigated-not-yet-built)).

```
  ┌──────────────────────────  Quest 3 (standalone app)  ──────────────────────────┐
  │  Hands ▸ GANTASMO XR MIDI Surface (sliders / knobs / buttons)                   │
  │            │                                   ▲                                │
  │            ▼                                   │                                │
  │      QuestMidiSender  ──TCP 127.0.0.1──┐   GANTASMO Visor (reacts to return MIDI)│
  └────────────────────────────────────────┼──────────────────────────────────────┘
                                            │  adb reverse over USB-C
                ┌───────────────────────────┴───────────────────────────┐
                ▼                                                         ▲
        bridge.js (Node, desktop)                                bridge.js (return)
                │                                                         ▲
                ▼                                                         │
     loopMIDI "QuestMIDI"  ──▶  theDAW (WebMIDI)  ──▶  loopMIDI "QuestMIDI-Return"
```

## Modules

| Folder | What it is | Docs |
|---|---|---|
| `Assets/QuestMidiBridge/` | The send path: `QuestMidiSender` + the Node desktop bridge + Setup Wizard | [README](Assets/QuestMidiBridge/README.md) |
| `Assets/QuestMidiBridge/Runtime/ControlSurface/` | The floating 3D hand-tracked MIDI surface (default 8 sliders, 6 knobs, 12 buttons), built from an editable config preset | [README](Assets/QuestMidiBridge/Runtime/ControlSurface/README.md) |
| `Assets/QuestMidiBridge/Bridge~/` | The desktop Node bridge (the `~` keeps it out of the APK) | [README](Assets/QuestMidiBridge/Bridge~/README.md) |
| `Assets/GantasmoVisor/` | The receive path: a MIDI-reactive chrome visor driven by the DAW's return MIDI | [README](Assets/GantasmoVisor/README.md) |
| `Assets/Editor/AndroidBuildFixes.cs` | Android/Gradle build workarounds for this project | — |

## Requirements

- **Unity 6.x** (this project: URP 17.4.0) on **Windows**.
- **Meta XR SDK** `com.meta.xr.sdk.all` 203.0.0 (Interaction SDK + Building Blocks).
- A **Quest 3** in Developer Mode, Horizon OS **v203**.
- **loopMIDI** (Tobias Erichsen) for the virtual MIDI ports.
- **Node.js** for the desktop bridge; **adb** (Android platform-tools) for the USB tunnel.
- A **WebMIDI DAW** (theDAW) to receive the `QuestMIDI` input.

## Quick start

1. Open the project in Unity and load `Assets/Scenes/QuestMIDI.unity`.
2. **Window ▸ Quest MIDI Bridge ▸ Setup Wizard** and work down the checklist:
   - Quest developer-mode + USB debugging.
   - Create two loopMIDI ports: **`QuestMIDI`** (Quest → DAW) and
     **`QuestMIDI-Return`** (DAW → visor).
   - Detect Node + adb, save the bridge config, start the bridge.
   - **Step 8 – GANTASMO surface:** Build the XR MIDI surface (or Repair an existing one),
     then **Validate scene wiring**.
   - **Step 9 – Diagnostics:** confirm build target is Android and set a real Company
     Name / Package Name before a production build.
3. In theDAW's WebMIDI device list, select **QuestMIDI** as input. Map controls with MIDI-learn.
4. Build & deploy the Android app to the Quest, or press **Play** in the Editor to test
   the whole chain on your desk first (see below).

## Testing without the headset

Press **Play** in the Editor with the bridge running. `QuestMidiSender` connects to the
PC's own `127.0.0.1`, so the full path (Unity → bridge → loopMIDI → DAW) works at your
desk. `adb reverse` only matters once the app runs *on the headset*.

## Troubleshooting

**Sliders/knobs don't move or send MIDI (buttons work).**
The surface's sliders/knobs need a `HandGrabInteractable` — the Building Blocks
comprehensive rig has hand-grab + poke interactors but **no plain `GrabInteractor`**, so
a bare `GrabInteractable` is never grabbed and the handle never moves. Run
**`GANTASMO ▸ Repair XR MIDI Surface Interactions`** (or the wizard's *Repair Interactions*
button). Freshly built surfaces already include it. Details:
[ControlSurface README](Assets/QuestMidiBridge/Runtime/ControlSurface/README.md#sliderssknobs-do-nothing-repair-an-older-surface).

**No MIDI reaches the DAW.** Confirm the bridge console is running, the loopMIDI port
name matches the wizard exactly, and `adb reverse tcp:8765 tcp:8765` is set (wizard has a
one-click button). The sender's TCP port must match `bridge-config.json`.

**Visor doesn't react.** It listens on the **return** port; make sure `QuestMIDI-Return`
exists and the DAW is sending to it on the channel the visor expects.

## Video feed (investigated, not yet built)

Goal: stream the headset's **mixed-reality POV** (passthrough + the 3D objects) to stage
monitors driven by the same PC as theDAW, ideally **without** Meta Quest Developer Hub
(MQDH) in the path. Status of the investigation:

- **theDAW's VJ sidecar (VJ-9000) has no network video ingest today.** Its only camera
  input is `getUserMedia` (an OS-level camera device) plus file clips. So any feed has to
  reach the host either as a **virtual webcam** or via an **ingest path that must first be
  added to the VJ**.
- **Works now (with MQDH):** MQDH/native casting mirrors the headset → capture that window
  in OBS → **OBS Virtual Camera** → it appears in the VJ's camera list. Zero code, but
  carries MQDH + OBS latency.
- **Bypassing MQDH is not impossible** — it's an engineering task on code we own. Because
  the app owns its frames, it can emit video itself (WebRTC sender from the app + a WebRTC
  receiver added to the VJ; signaling over the existing backend). The genuinely hard part
  for *this* POV is that **passthrough is composited by the OS, not in the app's
  framebuffer**, so capturing the true MR view means either OS-level capture or pulling the
  real camera frames via Meta's **Passthrough Camera API** (available on v203) and
  re-compositing in-app before streaming.

This is design-only so far; nothing here ships video yet.

## Known limitations / pre-release notes

- **Not a UPM package** (no asmdefs). It ships as a Unity project + desktop bridge.
- `Bridge~/node_modules/` is present for local runs — **exclude it from any release artifact**
  (the bridge re-installs from `package.json`).
- `Bridge~/package.json` floats `@julusian/midi`; pin it before cutting a release.
- App identity defaults (`DefaultCompany`, `com.UnityTechnologies…urpblank`) must be set.
- This is a Unity Version Control (Plastic) workspace, **not** Git — exact change history
  needs the Plastic CLI (`cm`) on PATH.
</content>
</invoke>
