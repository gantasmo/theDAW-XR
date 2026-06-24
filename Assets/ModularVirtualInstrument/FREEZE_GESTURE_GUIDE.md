# Freeze Gesture System

## Overview
The freeze gesture allows users to lock effect values at their current position by performing a palm-up gesture while controlling a stem.

## How It Works

### Gesture Detection
- **Trigger**: Flip palm facing upward (70° from horizontal by default)
- **Hold Time**: 0.5 seconds (configurable)
- **Toggle**: Holding palm up toggles freeze on/off

### Visual Feedback
1. **Wireframe Color**: Changes from theme color to cyan when frozen
2. **Pulse Speed**: Slows down to 0.3Hz when frozen (vs normal speed)
3. **GUI Overlay**: Shows "[FROZEN]" label in cyan next to stem name

### Effect Behavior
- **When Frozen**: Hand position updates don't affect effect values
- **When Unfrozen**: Normal hand tracking resumes
- **Persistence**: Frozen values maintain until unfrozen

## Inspector Settings

### UnifiedHandTrackingManager
```
[Freeze Gesture]
├── Enable Freeze Gesture: true/false
├── Freeze Angle Threshold: 45-90° (default: 70°)
└── Freeze Hold Time: 0.1-2.0s (default: 0.5s)
```

### Configuration
- **freezeAngleThreshold**: How far palm must point up (70° = nearly flat)
- **freezeHoldTime**: Duration to hold gesture before triggering

## Usage Examples

### Scenario 1: Lock Pitch While Changing Filter
1. Move hand to desired pitch position (Y-axis)
2. Flip palm upward and hold for 0.5s
3. Wireframe turns cyan → pitch is frozen
4. Move hand to adjust filter (Z-axis) without affecting pitch
5. Flip palm up again to unfreeze

### Scenario 2: Set Multiple Stems
1. Enter stem A, position effects
2. Freeze with palm-up gesture
3. Exit stem A (frozen values persist)
4. Enter stem B, position effects
5. Both stems maintain independent frozen states

## Technical Implementation

### Hand State Tracking
```csharp
private class HandState {
    public Vector3 palmNormal;
    public float palmUpAngle;
    public float freezeGestureTimer;
    public bool isPalmUp;
    // ... other fields
}
```

### Palm Normal Calculation
- Left hand: `wristPose.rotation * Vector3.left`
- Right hand: `wristPose.rotation * Vector3.right`
- Angle: `Vector3.Angle(palmNormal, Vector3.up)`

### Freeze Logic
```
palmUpAngle < (90° - threshold) → Palm facing up
Hold for freezeHoldTime → Toggle freeze state
```

## Debug Information
- **Log Hand Events**: Enable in UnifiedHandTrackingManager to see freeze triggers
- **GUI Overlay**: Shows frozen state and current effect values
- **Gizmo Color**: Frozen stems appear cyan in Scene View

## Tips
- **Adjust Threshold**: Lower angle = stricter palm-up requirement
- **Hold Time**: Shorter time = quicker freeze, but may trigger accidentally
- **Visual Cues**: Cyan color and slow pulse clearly indicate frozen state
- **Per-Stem**: Each stem maintains independent freeze state
