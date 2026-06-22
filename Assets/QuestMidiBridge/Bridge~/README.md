# Quest MIDI Bridge (desktop side)

This little Node program is a **two-way** bridge between the Quest Unity app and
your WebMIDI DAW, over the USB-C cable.

```
OUT  Quest app --TCP--> [adb reverse over USB] --> bridge.js --> loopMIDI "QuestMIDI"        --> DAW
IN   DAW --> loopMIDI "QuestMIDI-Return" --> bridge.js --> [adb reverse over USB] --TCP--> Quest app
```

The **IN** path completes the circuit: whatever the DAW writes to the return port
is forwarded to the headset, where any app-side receiver can react to it live.

## One-time setup
1. Install **Node.js**: https://nodejs.org (LTS is fine).
2. Install **loopMIDI**: https://www.tobias-erichsen.de/software/loopmidi.html
   - Create a port named exactly **`QuestMIDI`** (Quest → DAW).
   - Create a SECOND port named **`QuestMIDI-Return`** (DAW → Quest) for the
     return circuit. Use a different name — sharing one port would echo the
     Quest's own data back to it.
   - Both names come from the Unity Setup Wizard / `bridge-config.json`.
3. Install **adb** (Android platform-tools). If you have Unity's Android Build
   Support it's already on disk at `<AndroidSDK>/platform-tools/adb.exe`.

> The Unity **Setup Wizard** (Window ▸ Quest MIDI Bridge) automates most of this,
> including writing `bridge-config.json` and launching this bridge.

## Every session
Just double-click **`start-bridge.bat`**. It will:
- `npm install` the first time (downloads a prebuilt MIDI binary, no compiler needed),
- run `adb reverse` to open the USB tunnel,
- open the loopMIDI port and start listening.

Keep the window open while you perform. Press `Ctrl+C` to stop.

## bridge-config.json
| key             | meaning |
|-----------------|---------|
| `tcpPort`       | must match the port on the Unity `QuestMidiSender` component |
| `midiPortName`  | substring of the loopMIDI OUTPUT port to open (Quest → DAW) |
| `midiInPortName`| substring of the loopMIDI INPUT port to open (DAW → Quest, return circuit) |
| `adbPath`       | `adb` (if on PATH) or a full path to `adb.exe` |
| `autoAdbReverse`| run `adb reverse` automatically on startup |
| `verbose`       | log every MIDI message (hex) for debugging |

## Troubleshooting
- **"No port matching QuestMIDI"** → create that port in loopMIDI (the bridge keeps retrying, so just make it).
- **"adb reverse FAILED"** → plug in USB-C, accept *Allow USB debugging* on the headset, confirm `adb devices` lists it.
- **DAW doesn't see the port** → WebMIDI reads OS MIDI ports; loopMIDI must be running and the DAW page may need a refresh / `requestMIDIAccess()` re-query.
- **Editor Play mode test** → the Unity app connects to the PC's localhost directly, so you can test the whole chain *without* the headset or adb.
