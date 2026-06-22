# GANTASMO Colocation

Co-located multiplayer for the `QuestMIDI` scene. Several Quest 3 headsets in one room
share a single world frame, so the MIDI surface and the GANTASMO visuals lock to the
same physical spot for everyone, and each performer sees the others as networked
presence. A one-click setup wizard does the wiring.

```
  Headset A  ──┐
  Headset B  ──┤   Meta Colocation Discovery  (shared frame + group anchors)
  Headset C  ──┘                 +
                     Netcode for GameObjects over Unity Transport
                     (LAN-direct, no cloud relay, no external account)
                                  |
                                  v
            shared MIDI surface  +  networked head and 2-hand presence
```

## What it gives you

- **One shared frame.** Every headset aligns to the same real-world origin through
  Meta Colocation Discovery and group-shared spatial anchors, so the surface and
  visuals occupy the same physical place in the room.
- **Peer presence.** Each headset broadcasts a lightweight networked head and two hand
  proxies, so performers see each other. Full Meta Avatars are deferred because the
  project runs the OpenXR loader.
- **LAN-direct netcode.** Unity Netcode for GameObjects (NGO) over Unity Transport
  carries the light presence and interaction traffic. There is no cloud relay, no
  Photon, and no external account. The existing ADB sockets keep carrying video and
  MIDI, so the netcode only handles presence.

## Easy setup

Run **`Window > GANTASMO Colocation > Setup Wizard`** on the project, then on each
headset. The wizard:

- installs and places the Meta building blocks (Network Manager, Colocation, Local
  Matchmaking, Player Name Tag, MR Utility Kit),
- patches the Android manifest (colocation discovery, IoT map data, internet),
- sets the OVR project config to require shared-anchor and colocation-session support,
- reparents the MIDI surface under `ColocationRoot` and builds the
  `NetworkedPresence` prefab.

`ColocationRoot` is the anchored parent that everything aligns to. `NetworkedPresence`
is the NGO `NetworkBehaviour` that syncs the head and two hands as network variables in
world space, drawn with a shared material.

## Manual gates (Meta platform, cannot be automated)

- **Enhanced Spatial Services** enabled on each headset.
- A **verified Meta developer account** with test users.
- All headsets on the **same Wi-Fi network**.

Everything else (packages, manifest, OVR features, building blocks, scene wiring) is
driven by the wizard.

## Status

Built and wired in the editor: building blocks installed and verified in scene,
`NetworkManager` bound to `UnityTransport` with `NetworkedPresence` as the player
prefab, and the MIDI surface reparented under `ColocationRoot`. The console is clean.
On-headset verification of the shared frame and peer presence needs a second charged
headset and the manual gates above, so alignment accuracy is not yet confirmed on
hardware. Plan: `docs/plans/2026-06-18-quest-colocation-plan.md` in theDAW repo.
