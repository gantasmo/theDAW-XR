# Quest MIDI Bridge

Send MIDI from a **standalone Quest 3 Unity app** to a **WebMIDI desktop DAW**,
over the **USB-C cable only** (no Wi-Fi). Built for Unity 6.4 on Windows.

```
Quest hand interactions
      |
      v
QuestMidiSender (TCP, 127.0.0.1)
      |   ... adb reverse tunnel over the USB-C cable ...
      v
bridge.js (desktop)  -->  loopMIDI virtual port  -->  your DAW (WebMIDI)
```

## Fastest path
1. In Unity: **Window ▸ Quest MIDI Bridge ▸ Setup Wizard**. Work down the checklist
   (it detects Node + adb, writes the config, installs deps, and starts the bridge).
2. Click **Add MIDI Sender to scene** in the wizard, then call it from your hand code:

   ```csharp
   sender.SendNoteOn(60, 110);      // pinch -> note
   sender.SendControlChange(1, 64); // 0-127 CC
   sender.SendControlChange14(1, t); // smooth 14-bit CC (great for hand motion)
   ```
   See `HandMidiExample.cs` for a working mapper.
3. In your DAW, pick the **QuestMIDI** input in your WebMIDI device list. Done.

## Testing without the headset
Press **Play** in the Editor with the bridge running — the app talks to the PC's
localhost directly, so the full chain (Unity → bridge → loopMIDI → DAW) works on
your desk before you ever deploy to the Quest. `adb reverse` only matters once the
app runs *on the headset*.

## Why this design
- **Zero DAW changes** — loopMIDI shows up as a normal MIDI input to WebMIDI.
- **USB-only, offline** — `adb reverse` rides the Link cable; no network needed on stage.
- **Low latency** — Nagle disabled both ends; adb-over-USB adds ~1 ms.
- **MIDI resolution tip** — hand motion through 7-bit CC (0-127) can feel steppy;
  use `SendControlChange14` for smooth control.

The Node side lives in `Bridge~/` (the `~` means Unity ignores it — it's never
imported and never ends up in your APK).
