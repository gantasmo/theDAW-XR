# Flexalon Pro MVI - Quick Reference

## 🎛️ All 7 Layout Types at Your Fingertips

### In ModularSynthController Inspector:

```
┌─ Flexalon Layout Controls ─────────────┐
│ Layout Type: [Select one ▼]             │
│  • Grid      - Organized rows/columns   │
│  • Flexible  - Linear with wrapping     │
│  • Circle    - Circular/spiral          │
│  • Curve     - Follow animation curve   │
│  • Align     - All at same position     │
│  • Random    - Seeded randomization     │
│  • Shape     - Geometric patterns       │
└─────────────────────────────────────────┘
```

## 📋 Parameter Quick Reference

### Grid
- Columns: 1-10
- Rows: 1-10
- Column Spacing: 0.1-5m
- Row Spacing: 0.1-5m

### Flexible
- Direction: 6 options (±X, ±Y, ±Z)
- Gap: 0-5m
- Wrap: Yes/No
- Wrap Direction: 6 options

### Circle
- Radius: 0.5-10m
- Start Angle: 0-360°
- Spacing: 1-90°
- Spiral: Yes/No
- Spiral Spacing: 0.1-2m

### Curve
- Path: AnimationCurve
- Length: 1-20m
- Spacing: 0.1-5m

### Align
- H/V/D Align: Start/Center/End

### Random
- Bounds Min/Max: Vector3
- Seed: Int (with randomize button)

### Shape
- Type: Circle/Square/Pentagon/Hexagon/Star
- Radius: 0.5-10m
- Sides: 3-12 (polygons only)

## 🎬 Common Workflows

### Create New Layout
1. Right-click → Create → MVI → Layout → Flexalon Adapter
2. Assign to controller's Layout Strategy
3. Select layout type in inspector
4. Adjust parameters
5. Click "Generate Stems"

### Switch Between Layouts
1. Select layout type from dropdown
2. Parameters update automatically
3. Click "Regenerate Layout" to apply

### Fine-Tune Individual Stems
1. Click "Add Runtime Editors"
2. Per-stem position controls appear
3. Adjust X/Y/Z positions individually
4. Click "Save Current Layout" to persist

### Animate Between Layouts
1. Enable "Use Animations"
2. Set Animation Speed (1-20)
3. Use LayoutSwitcher component for runtime switching

## 🎯 Pro Tips

**Grid**: Use 3x2 for drum kits, 4x4 for large synthesizers
**Flexible**: Enable wrap for responsive toolbar-like layouts
**Circle**: Use spiral mode for vertical space utilization
**Curve**: Combine multiple curves with Align layout for complex paths
**Random**: Use same seed for reproducible "organic" layouts
**Shape**: Star pattern with 10 points creates nice melodic layouts

All layouts work with RuntimeLayoutEditor for per-stem customization!
