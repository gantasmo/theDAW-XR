# GANTASMO Passthrough Stitch

Streams the Quest's real-world passthrough into theDAW's VJ engine as a clean live
video source called **STITCH**, over plain ADB. No Quest Link tether and no Meta
Quest Developer Hub casting are in the path, and the desktop runs no OBS or scrcpy.

```
  two forward RGB passthrough cameras
            |
            v
  GantasmoPassthroughStitch   (reproject + feather-blend into one 16:9 frame,
            |                   optional 3D virtual content composited on top)
            v
  GantasmoStitchStreamer       (Android MediaCodec H.264, Annex-B over TCP)
            |
            |  adb reverse tcp:8940 tcp:8940
            v
  theDAW  queststitch module   (TCP -> WebSocket re-frame)
            |
            v
  VJ  cameraSource 'queststitch'  ==  the STITCH source  (WebCodecs decode)
```

## What it is, and how it differs from delinQuest

Both features put the headset on the VJ wall over ADB, with neither MQDH nor Quest
Link. They show different things:

- **delinQuest** (the `questcast` module) mirrors the *entire headset display*: the
  mixed-reality scene plus the performer's overlaid MIDI surface. It captures the
  rendered view.
- **STITCH** (this module) streams *only the clean real-world composite*: the stitched
  forward passthrough, without the overlaid surface. An optional layer composites the
  app's own 3D content back in, so the stream can carry augmented scenery while
  staying free of the control overlay.

## Setup

1. Run **`GANTASMO > Add Passthrough Stitch To Scene`**. The builder adds both
   `GantasmoPassthroughStitch` and `GantasmoStitchStreamer` to one GameObject and
   wires the streamer to the stitch. Re-running on an existing stitch adds the
   streamer if it is missing.
2. Save the scene so the components are included in a headset build.
3. Enable theDAW's **`queststitch`** backend module.
4. In theDAW's VJ source list, select **STITCH**. The backend runs `adb reverse` and
   opens the bridge, then the picture appears.

The headset only needs USB debugging enabled, or a wireless ADB pairing.

## Components

`GantasmoPassthroughStitch` self-bootstraps the two `PassthroughCameraAccess`
instances (left and right forward RGB), reprojects them onto a plane at
`focalDistanceMeters`, feather-blends the seam over `blendWidth`, and writes a 16:9
`OutputTexture`. With `composite3D` on, a head-centred capture camera renders the
app's virtual content (matte) through `PassthroughComposite.shader` over the
passthrough, at `augmentationResolution` and limited by `augmentationCullingMask`.
`useEnvironmentDepth` switches the single reprojection plane for the real per-pixel
scene distance when depth is available.

`GantasmoStitchStreamer` encodes `CompositeTexture` (or `OutputTexture`) to H.264 with
Android MediaCodec and frames the Annex-B NALs over a localhost TCP socket. The codec
config packet is cached and re-sent before every keyframe, so any client that joins
late configures its decoder within one keyframe interval. The RGBA-to-NV12 conversion
runs as a Burst job to keep it off the render loop. On the desktop and in the editor it
connects and sends metadata but encodes no video, because MediaCodec is Android-only.

## Tuning (Inspector)

| Field | Default | Purpose |
|---|---|---|
| `streamResolution` | 1280 x 720 | encoded video size |
| `streamFps` | 30 | encoder frame rate |
| `bitrateKbps` | 6000 | encoder bitrate |
| `keyframeIntervalSec` | 1 | how often SPS/PPS and a keyframe are re-sent |
| `flipVertical` | on | GPU readback is bottom-up |
| `swapUV` | off | flip NV12/NV21 if colors are wrong on a given encoder |
| `host` / `port` | 127.0.0.1 / 8940 | must match the `queststitch` module |
| `focalDistanceMeters` | 1.5 | reprojection plane; tunes seam alignment |
| `blendWidth` | 0.12 | seam feather width |
| `outputResolution` | 1920 x 1080 | stitch render size before the stream scale |
| `composite3D` | on | overlay the app's 3D content |
| `augmentationResolution` | 1280 x 720 | render size of the 3D overlay layer |

Lower the resolutions and frame rate, or turn `composite3D` off, if the headset frame
rate drops. The MIDI surface and other content to be streamed must sit on the layer
selected by `augmentationCullingMask`.

## Status

Built and verified at the plumbing level (Unity compiles clean, the backend bridge and
the VJ decoder connect and pass metadata). The encoded picture runs only on a real
headset, so on-device color, alignment, and frame rate still need a live tuning pass.
