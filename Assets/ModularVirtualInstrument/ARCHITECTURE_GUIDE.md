# Modular Virtual Instrument (MVI) - System Architecture & User Interface Guide

## 📐 SYSTEM ARCHITECTURE FLOWCHART

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          MODULAR VIRTUAL INSTRUMENT                          │
│                           System Architecture Flow                           │
└─────────────────────────────────────────────────────────────────────────────┘

                              ┌──────────────────┐
                              │  User/Designer   │
                              │   Configures:    │
                              │  - Stem Data     │
                              │  - Layout        │
                              │  - Effects       │
                              └────────┬─────────┘
                                       │
                                       ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                     MODULAR SYNTH CONTROLLER (Main Hub)                      │
│  ┌────────────────────────────────────────────────────────────────────┐     │
│  │ Responsibilities:                                                   │     │
│  │ • Load StemData[] from inspector or runtime                        │     │
│  │ • Calculate positions using StemLayoutStrategy                     │     │
│  │ • Instantiate StemProcessor for each stem                          │     │
│  │ • Coordinate global playback (Play/Pause/Stop All)                 │     │
│  └────────────────────────────────────────────────────────────────────┘     │
└───────┬──────────────────────────────────────────────────────────────────────┘
        │
        │ Creates & Positions
        ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         STEM LAYOUT STRATEGY                            │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ SemicircularLayout (ScriptableObject)                            │  │
│  │ ┌──────────────────────────────────────────────────────────────┐ │  │
│  │ │ Global Controls:                                             │ │  │
│  │ │  • Radius                    - Size of semicircle           │ │  │
│  │ │  • Height Offset             - Base elevation               │ │  │
│  │ │  • Arc Rotation              - Rotate entire layout         │ │  │
│  │ │  • Arc Angle                 - 180° = semicircle            │ │  │
│  │ └──────────────────────────────────────────────────────────────┘ │  │
│  │ ┌──────────────────────────────────────────────────────────────┐ │  │
│  │ │ Per-Stem Controls (Array of StemPositionControl):           │ │  │
│  │ │  • Perimeter Position (0-1)  - Location on arc             │ │  │
│  │ │  • Height Adjustment         - Individual elevation         │ │  │
│  │ │  • Depth Offset             - Forward/backward from arc    │ │  │
│  │ │  • Additional Rotation       - Extra Y-axis rotation       │ │  │
│  │ └──────────────────────────────────────────────────────────────┘ │  │
│  │                                                                    │  │
│  │ Output: Vector3[] positions, Quaternion[] rotations               │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
        │
        │ Positions sent to each
        ▼
┌───────────────────────────────────────────────────────────────────────┐
│                          STEM PROCESSOR (x N)                         │
│  One instance per audio stem/track                                    │
│  ┌─────────────────────────────────────────────────────────────────┐ │
│  │ Components:                                                      │ │
│  │ • StemData (reference)     - Configuration asset                │ │
│  │ • AudioSource              - Plays the audio clip               │ │
│  │ • 3x AxisEffectSlot        - X, Y, Z effect processors          │ │
│  │ • Interaction Bounds       - 3D cube volume                     │ │
│  │ • StemVisualizer           - Visual feedback                    │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│                                                                        │
│  Flow:                                                                 │
│  1. Initialize from StemData                                          │
│  2. Setup AudioSource with clip                                       │
│  3. Create 3 AxisEffectSlots (X, Y, Z)                               │
│  4. Assign effects to each axis                                       │
│  5. Wait for hand tracking events                                     │
│  6. Process effects based on normalized hand position (0-1, 0-1, 0-1)│
└───┬───────────────────────────────────────────────────────────────────┘
    │
    │ Contains
    ▼
┌───────────────────────────────────────────────────────────────────────┐
│                         AXIS EFFECT SLOT (x 3)                        │
│  X-Axis (Red) | Y-Axis (Green) | Z-Axis (Blue)                        │
│  ┌─────────────────────────────────────────────────────────────────┐ │
│  │ Properties:                                                      │ │
│  │ • AxisEffect reference     - Assigned effect (ScriptableObject) │ │
│  │ • Current Value (0-1)      - Current parameter                  │ │
│  │ • Smoothed Value           - After smoothing applied            │ │
│  │ • Invert Axis              - Reverse direction if needed        │ │
│  │ • Smoothing Factor         - Reduces jitter                     │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│                                                                        │
│  When hand position updates:                                          │
│  1. Receive normalized value (0-1) for this axis                     │
│  2. Apply inversion if enabled                                        │
│  3. Apply smoothing                                                   │
│  4. Call effect.ProcessEffect(smoothedValue, audioSource, deltaTime) │
└───┬───────────────────────────────────────────────────────────────────┘
    │
    │ Uses
    ▼
┌───────────────────────────────────────────────────────────────────────┐
│                      AXIS EFFECT (ScriptableObject)                   │
│  Base class for all effects - modular and swappable                   │
│  ┌─────────────────────────────────────────────────────────────────┐ │
│  │ Virtual Methods:                                                 │ │
│  │ • ProcessEffect(float value, AudioSource, deltaTime)            │ │
│  │ • OnEffectEnabled(AudioSource)                                  │ │
│  │ • OnEffectDisabled(AudioSource)                                 │ │
│  │ • ProcessAudioFilter(float[] data, channels, value) [optional]  │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│                                                                        │
│  Implementations:                                                      │
│  • PitchEffect          - Changes playback speed/pitch               │
│  • BitCrushEffect       - Reduces bit depth (lo-fi glitch)           │
│  • StutterEffect        - Rhythmic muting/repetition                 │
│  • DelayEffect          - Echo/delay with feedback                   │
│  • FilterEffect         - Low/high pass frequency filter             │
│  • DistortionEffect     - Waveshaping distortion                     │
└────────────────────────────────────────────────────────────────────────┘


┌───────────────────────────────────────────────────────────────────────┐
│                    UNIFIED HAND TRACKING MANAGER                      │
│  Monitors hand positions and assigns to stems                         │
│  ┌─────────────────────────────────────────────────────────────────┐ │
│  │ Tracks:                                                          │ │
│  │ • Left Hand (Oculus.Interaction.Hand)                           │ │
│  │ • Right Hand (Oculus.Interaction.Hand)                          │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│                                                                        │
│  Every frame (at updateRate Hz):                                      │
│  1. Get index finger tip position for each hand                       │
│  2. Check which stem cubes contain the finger position                │
│  3. Assign hand to stem (with priority system)                        │
│  4. Call StemProcessor.OnHandEnter/Update/Exit                        │
│                                                                        │
│  Features:                                                             │
│  • ANY hand can control ANY stem                                      │
│  • Dual-hand control (both hands on different stems)                 │
│  • Optional multi-stem control (one hand on multiple overlapping)    │
│  • Assignment delay to prevent flickering                             │
└────────────────────────────────────────────────────────────────────────┘
        │
        │ Sends hand position to
        ▼
┌───────────────────────────────────────────────────────────────────────┐
│                         STEM VISUALIZER                               │
│  Creates neon wireframe cube with visual feedback                     │
│  ┌─────────────────────────────────────────────────────────────────┐ │
│  │ Visual Elements:                                                 │ │
│  │ • 12 LineRenderers       - Wireframe cube edges                 │ │
│  │ • 3 LineRenderers        - X/Y/Z axis indicators (RGB)          │ │
│  │ • Neon glow/pulse        - Emission + animated intensity        │ │
│  │ • Hand trail (optional)  - Particle trail following hand        │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│                                                                        │
│  Dynamic Behavior:                                                     │
│  • Cube color = StemData.themeColor                                   │
│  • Brightness increases when hand enters                              │
│  • Axis indicators show current effect values (color changes)         │
│  • Pulse effect synchronized with beat (optional)                     │
└────────────────────────────────────────────────────────────────────────┘


┌───────────────────────────────────────────────────────────────────────┐
│                            STEM DATA                                  │
│  Configuration asset (ScriptableObject) - one per stem                │
│  ┌─────────────────────────────────────────────────────────────────┐ │
│  │ Audio Configuration:                                             │ │
│  │ • AudioClip                - The audio file to play             │ │
│  │ • Default Volume           - Initial volume (0-1)               │ │
│  │ • Loop Audio               - Loop the clip?                     │ │
│  │ • Output Mixer Group       - Audio routing                      │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│  ┌─────────────────────────────────────────────────────────────────┐ │
│  │ Effect Assignment:                                               │ │
│  │ • X-Axis Effect            - Effect for horizontal movement     │ │
│  │ • Y-Axis Effect            - Effect for vertical movement       │ │
│  │ • Z-Axis Effect            - Effect for depth movement          │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│  ┌─────────────────────────────────────────────────────────────────┐ │
│  │ Visual Configuration:                                            │ │
│  │ • Theme Color              - Primary neon color                 │ │
│  │ • Accent Color             - Secondary color                    │ │
│  │ • Emission Intensity       - Glow brightness                    │ │
│  │ • Cube Size                - Interaction volume size            │ │
│  └─────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 🎮 USER INTERFACE GUIDE

### **In-Editor Setup (Unity Inspector)**

#### **Step 1: Create Stem Data Assets**
```
Right-click in Project > Create > Modular Virtual Instrument > Stem Data

Configure each stem:
├─ Stem Identity
│  ├─ Stem Name: "Drums"
│  └─ Description: "Drum loop for the track"
│
├─ Audio
│  ├─ Audio Clip: [Drag your .wav/.mp3 file]
│  ├─ Default Volume: 0.7
│  ├─ Loop Audio: ✓
│  └─ Output Mixer Group: (optional)
│
├─ Default Effects (Assign AxisEffect assets - created later)
│  ├─ X-Axis Effect: PitchEffect
│  ├─ Y-Axis Effect: FilterEffect
│  └─ Z-Axis Effect: StutterEffect
│
├─ Interaction Volume
│  ├─ Cube Size: (1, 1, 1) - Default
│  └─ Hand Tracking Smoothing: 0.8
│
└─ Visual Appearance
   ├─ Theme Color: Cyan (RGB: 0, 255, 255)
   ├─ Accent Color: White
   ├─ Emission Intensity: 2.0
   └─ Show Waveform: ✓
```

#### **Step 2: Create Layout Strategy**
```
Right-click > Create > Modular Virtual Instrument > Layout > Semicircular Layout

Configure SemicircularLayout:
├─ Semicircle Settings
│  ├─ Radius: 2.0 (meters from center)
│  ├─ Height Offset: 0.0 (eye level)
│  ├─ Arc Rotation: 0° (facing forward)
│  └─ Arc Angle: 180° (full semicircle)
│
├─ Per-Stem Controls (auto-generated when stems load)
│  ├─ Stem 0 (e.g., "Drums")
│  │  ├─ Perimeter Position: 0.0 (far left)
│  │  ├─ Height Adjustment: 0.0
│  │  ├─ Depth Offset: 0.0 (on arc)
│  │  └─ Additional Rotation: 0°
│  │
│  ├─ Stem 1 (e.g., "Bass")
│  │  ├─ Perimeter Position: 0.33 (left-center)
│  │  ├─ Height Adjustment: -0.2 (slightly lower)
│  │  ├─ Depth Offset: 0.5 (closer to user)
│  │  └─ Additional Rotation: 0°
│  │
│  ├─ Stem 2 (e.g., "Melody")
│  │  ├─ Perimeter Position: 0.66 (right-center)
│  │  ├─ Height Adjustment: 0.2 (slightly higher)
│  │  ├─ Depth Offset: -0.3 (farther from user)
│  │  └─ Additional Rotation: 0°
│  │
│  └─ Stem 3 (e.g., "Vocals")
│     ├─ Perimeter Position: 1.0 (far right)
│     ├─ Height Adjustment: 0.0
│     ├─ Depth Offset: 0.0
│     └─ Additional Rotation: 0°
│
└─ Auto-Configuration
   ├─ Auto Create Controls: ✓ (creates controls for each stem)
   └─ Face Center: ✓ (cubes look at user)
```

#### **Step 3: Setup Main Scene**
```
1. Create Empty GameObject: "MVI_System"
   
2. Add Component: ModularSynthController
   ├─ Stem Configuration
   │  └─ Stems (Array)
   │     ├─ Size: 4
   │     ├─ Element 0: Drums_StemData
   │     ├─ Element 1: Bass_StemData
   │     ├─ Element 2: Melody_StemData
   │     └─ Element 3: Vocals_StemData
   │
   ├─ Layout
   │  ├─ Layout Strategy: SemicircularLayout asset
   │  ├─ Layout Center: (0, 1.5, 2) - in front of user
   │  └─ Use Local Space: ✓
   │
   ├─ Stem Processor
   │  └─ Stem Processor Prefab: (optional - leave empty for auto-creation)
   │
   ├─ Auto-Start
   │  └─ Auto Initialize: ✓
   │
   └─ Debug
      ├─ Show Debug Info: ✓
      └─ Show Layout Gizmos: ✓ (see layout in Scene view)

3. Add Component: UnifiedHandTrackingManager
   ├─ Hand Tracking
   │  ├─ Left Hand: [Drag OVRHand or Hand component]
   │  └─ Right Hand: [Drag OVRHand or Hand component]
   │
   ├─ Controller Reference
   │  └─ Synth Controller: [Drag ModularSynthController component]
   │
   ├─ Tracking Settings
   │  ├─ Allow Dual Hand Control: ✓
   │  ├─ Allow Multi-Stem Control: □
   │  ├─ Update Rate: 60 Hz
   │  └─ Assignment Delay: 0.1s
   │
   └─ Debug
      ├─ Show Debug Rays: ✓
      └─ Log Hand Events: □ (enable for debugging)
```

---

### **Runtime User Experience (AR/VR)**

#### **What the User Sees:**

```
         User's View in VR/AR Headset
┌────────────────────────────────────────────┐
│                                            │
│         ┌─────┐      ┌─────┐      ┌─────┐│
│         │ 🟦  │      │ 🟩  │      │ 🟥  ││  ← Neon wireframe cubes
│         │Drums│      │Bass │      │Vocal││     floating in semicircle
│         └─────┘      └─────┘      └─────┘│
│            ↑            ↑            ↑    │
│         Cyan         Green          Red   │  ← Theme colors
│                                            │
│              (Hand reaches toward cube)   │
│                      👋                   │
│                                            │
│  Each cube shows:                          │
│  • Wireframe edges (12 lines)             │
│  • RGB axis indicators (X=Red, Y=Green,   │
│    Z=Blue lines)                           │
│  • Pulsing glow effect                     │
│  • Brighter when hand is inside            │
└────────────────────────────────────────────┘
```

#### **Interaction Flow:**

1. **Approaching a Cube:**
   - User extends index finger toward any cube
   - Cube brightens as hand gets closer
   - Axis indicators appear

2. **Entering the Cube:**
   - Hand enters the interaction volume (cube bounds)
   - `UnifiedHandTrackingManager` detects collision
   - Cube assigned to that hand
   - `StemProcessor.OnHandEnter()` triggered
   - Visual feedback: Extra glow + hand trail appears

3. **Controlling Effects (Inside Cube):**
   ```
   Hand Position Mapping:
   
   X-Axis (Left ←→ Right):
   • 0.0 = Far left of cube    → Effect minimum
   • 0.5 = Center              → Effect middle
   • 1.0 = Far right of cube   → Effect maximum
   Example: Pitch bending from low to high
   
   Y-Axis (Down ↕ Up):
   • 0.0 = Bottom of cube      → Effect minimum
   • 0.5 = Middle              → Effect middle
   • 1.0 = Top of cube         → Effect maximum
   Example: Filter cutoff from dark to bright
   
   Z-Axis (Back ↔ Front):
   • 0.0 = Back of cube        → Effect minimum
   • 0.5 = Middle              → Effect middle
   • 1.0 = Front of cube       → Effect maximum
   Example: Stutter rate from slow to fast
   ```

4. **Visual Feedback While Controlling:**
   - Red line (X-axis indicator) extends based on horizontal position
   - Green line (Y-axis indicator) extends based on vertical position
   - Blue line (Z-axis indicator) extends based on depth position
   - Line colors change intensity based on effect value
   - Entire cube pulses in sync with audio (if beat detection enabled)

5. **Leaving the Cube:**
   - Hand exits the volume
   - `StemProcessor.OnHandExit()` triggered
   - Effects freeze at last value OR reset (configurable)
   - Glow returns to normal
   - Hand trail disappears

6. **Multi-Stem Control:**
   - Left hand in "Drums" cube → controlling drums
   - Right hand in "Bass" cube → controlling bass
   - Both stems play simultaneously with independent effects
   - Each hand sees its own visual feedback

---

### **Inspector Controls - Detailed Breakdown**

#### **Per-Stem Layout Adjustment (Semicircular Layout)**

**Use Case:** You have 4 stems arranged in a semicircle, but want to customize individual positions.

```
Inspector View: SemicircularLayout Asset

┌───────────────────────────────────────────────┐
│ Stem Controls (Array)                         │
│ ├─ Size: 4                                    │
│ │                                              │
│ ├─ Element 0: "Drums" Stem                   │
│ │  ├─ Perimeter Position: [━━━━━━━━━━] 0.0   │  ← Slider: 0 = start of arc
│ │  │                       (Far Left)          │
│ │  ├─ Height Adjustment:  [━━━━━━━━━━] 0.0   │  ← Slider: Raise/lower
│ │  ├─ Depth Offset:      [━━━━━━━━━━] 0.0   │  ← Slider: Push/pull from arc
│ │  └─ Additional Rotation: [━━━━━━━━━━] 0°   │  ← Slider: Spin the cube
│ │                                              │
│ ├─ Element 1: "Bass" Stem                    │
│ │  ├─ Perimeter Position: [━━━━━━━━━━] 0.33  │  ← 1/3 along arc
│ │  ├─ Height Adjustment:  [━━━━━━━━━━] -0.2  │  ← Lowered slightly
│ │  ├─ Depth Offset:      [━━━━━━━━━━] 0.5   │  ← Pulled 0.5m closer
│ │  └─ Additional Rotation: [━━━━━━━━━━] 15°  │  ← Rotated 15° clockwise
│ │                                              │
│ ├─ Element 2: "Melody" Stem                  │
│ │  ├─ Perimeter Position: [━━━━━━━━━━] 0.66  │
│ │  ├─ Height Adjustment:  [━━━━━━━━━━] 0.2   │
│ │  ├─ Depth Offset:      [━━━━━━━━━━] -0.3  │  ← Pushed 0.3m away
│ │  └─ Additional Rotation: [━━━━━━━━━━] 0°   │
│ │                                              │
│ └─ Element 3: "Vocals" Stem                  │
│    ├─ Perimeter Position: [━━━━━━━━━━] 1.0   │  ← End of arc (far right)
│    ├─ Height Adjustment:  [━━━━━━━━━━] 0.0   │
│    ├─ Depth Offset:      [━━━━━━━━━━] 0.0   │
│    └─ Additional Rotation: [━━━━━━━━━━] 0°   │
└───────────────────────────────────────────────┘
```

**What Each Control Does:**

1. **Perimeter Position (0.0 - 1.0)**
   - Position along the semicircle arc
   - 0.0 = Leftmost position (-90° if arc is 180°)
   - 0.5 = Center position (0° forward)
   - 1.0 = Rightmost position (+90°)
   - Even spacing: For 4 stems, use 0.0, 0.33, 0.66, 1.0

2. **Height Adjustment (-2.0 to +2.0)**
   - Vertical offset in meters
   - Negative = Lower than base height
   - Positive = Higher than base height
   - Example: Bass at -0.2m (waist level), Vocals at +0.3m (head level)

3. **Depth Offset (-2.0 to +2.0)**
   - Radial distance from the arc
   - Negative = Pushed away (farther from user)
   - Positive = Pulled closer (toward user)
   - Relative to the arc position (not absolute)
   - Example: Important stems (vocals) at +0.5m closer for easier reach

4. **Additional Rotation (-180° to +180°)**
   - Extra Y-axis rotation
   - 0° = Default (facing center if Face Center enabled)
   - Positive = Clockwise
   - Negative = Counter-clockwise
   - Use for aesthetic tweaking or to angle cubes differently

---

### **Effect Assignment Workflow**

```
How to assign different effects to different stems:

1. Create Effect Assets:
   Right-click > Create > MVI > Effects > Pitch Effect
   → Creates "PitchEffect_Asset.asset"
   
   Repeat for: FilterEffect, StutterEffect, DelayEffect, etc.

2. Configure Effect Parameters (in the effect asset):
   PitchEffect:
   ├─ Effect Name: "Pitch Shifter"
   ├─ Response Curve: [Animation curve - can be linear or custom]
   ├─ Effect Color: Yellow
   ├─ Min Pitch: 0.5 (half speed)
   └─ Max Pitch: 2.0 (double speed)

3. Assign to Stem Data:
   Open StemData asset → Default Effects section
   ├─ X-Axis Effect: Drag PitchEffect_Asset here
   ├─ Y-Axis Effect: Drag FilterEffect_Asset here
   └─ Z-Axis Effect: Drag StutterEffect_Asset here

4. Result at Runtime:
   User moves hand in "Drums" cube:
   • Left/Right (X) → Pitch changes
   • Up/Down (Y)    → Filter opens/closes
   • In/Out (Z)     → Stutter speed varies
```

---

## 🔄 DATA FLOW DIAGRAM

```
RUNTIME EXECUTION FLOW (Every Frame)

┌─────────────────────────────────────────────────────────────┐
│ UnifiedHandTrackingManager.Update()                         │
│ ↓                                                            │
│ 1. Get left/right hand index finger positions               │
│ 2. Check bounds.Contains(fingerPos) for all stem cubes      │
│ 3. Assign hands to stems (any hand → any stem)              │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ StemProcessor.OnHandUpdate(worldPosition)                   │
│ ↓                                                            │
│ 1. Convert world position → normalized (0-1, 0-1, 0-1)      │
│ 2. Extract X, Y, Z components                               │
│ 3. Call UpdateEffects(deltaTime)                            │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ AxisEffectSlot.UpdateEffect(normalizedValue, deltaTime)     │
│ ↓                                                            │
│ 1. Apply inversion if enabled: value = 1 - value            │
│ 2. Apply smoothing: smoothed = lerp(value, prev, factor)    │
│ 3. Call effect.ProcessEffect(smoothed, audioSource, dt)     │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ AxisEffect.ProcessEffect(value, audioSource, deltaTime)     │
│ ↓                                                            │
│ Example (PitchEffect):                                       │
│ • Map value through response curve                          │
│ • Calculate pitch: lerp(minPitch, maxPitch, mappedValue)    │
│ • Apply: audioSource.pitch = calculatedPitch                │
└─────────────────────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ Audio Output → Unity Audio System → Speakers/Headphones     │
└─────────────────────────────────────────────────────────────┘
```

---

## 📝 SUMMARY

### **Key Concepts:**

1. **Modular = Plug & Play**
   - Stems are independent
   - Effects are swappable ScriptableObjects
   - Layout strategies are interchangeable
   - No hardcoded connections

2. **Procedural Generation**
   - Add 1 stem → 1 cube appears
   - Add 10 stems → 10 cubes appear
   - Layout automatically calculates positions
   - No manual placement needed

3. **3-Axis Control**
   - Every stem has X, Y, Z control
   - Each axis can have ANY effect
   - Normalized (0-1) values make effects consistent
   - Visual feedback (RGB axis lines) shows current state

4. **Flexible Hand Tracking**
   - ANY hand controls ANY stem
   - Simultaneous dual-hand control
   - Priority-based assignment
   - Smooth transitions between stems

5. **Designer-Friendly**
   - All configuration in Unity Inspector
   - No coding required for setup
   - ScriptableObject assets = reusable presets
   - Real-time tweaking in Play mode

---

This architecture creates a **modular performance instrument** where musicians can load any number of audio stems and control them with intuitive 3D hand gestures in AR/VR space!
