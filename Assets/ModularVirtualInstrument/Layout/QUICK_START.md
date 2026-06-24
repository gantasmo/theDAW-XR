# Quick Start Guide - MVI Layout Editing

## 5-Minute Setup

### Option 1: Edit Mode Layout Design

**Goal:** Design and save an instrument layout without entering play mode

1. **Setup Controller**
   - Create empty GameObject named "MVI_Controller"
   - Add `ModularSynthController` component
   - Assign StemData assets to the `stems` array

2. **Create Layout**
   - Right-click in Project: Create → Modular Virtual Instrument → Layout → Semicircular Layout
   - Assign to controller's `layoutStrategy` field

3. **Generate Stems**
   - Select MVI_Controller in hierarchy
   - In Inspector, click **"Generate Stems in Edit Mode"**
   - Stems appear as children in scene

4. **Adjust Layout**
   - Use **Live Layout Controls** sliders to adjust:
     - Radius, Height, Arc Rotation, Arc Angle
   - Select individual stems from dropdown
   - Fine-tune per-stem position with sliders

5. **Save as Prefab**
   - Click **"Save as Instrument Prefab"**
   - Choose save location
   - Done! Prefab can be reused

---

### Option 2: Runtime Editing

**Goal:** Allow editing stem positions during play mode

1. **Setup (from Option 1 steps 1-3)**

2. **Add Runtime Editors**
   - Select MVI_Controller
   - Click **"Add Runtime Editors to All Stems"**

3. **Enter Play Mode**

4. **Edit Positions**
   - Select any stem GameObject
   - Find `RuntimeLayoutEditor` component
   - Adjust sliders:
     - Perimeter Position (0-1)
     - Height Adjustment
     - Depth Offset
     - Additional Rotation
   - Changes apply in real-time with smooth transitions

5. **Save Changes** (optional)
   - Call `SaveToLayout()` from script or inspector button
   - Changes persist to layout asset

---

### Option 3: Flexalon Layouts

**Goal:** Use Flexalon's powerful layouts for MVI

1. **Create Flexalon Adapter**
   - Right-click in Project: Create → Modular Virtual Instrument → Layout → Flexalon Adapter
   - Configure layout type:
     - **Grid** for organized rows/columns
     - **Circle** for radial arrangement
     - **Flexible** for responsive wrapping

2. **Configure Settings**
   - For Grid: Set columns, rows, spacing
   - For Circle: Set radius, start angle, spacing
   - Enable "Use Animators" for smooth transitions

3. **Assign to Controller**
   - Assign FlexalonLayoutAdapter to controller's `layoutStrategy`
   - Generate stems or regenerate layout

4. **Result**
   - Stems arranged using Flexalon's layout engine
   - Smooth animations if animators enabled

---

### Option 4: Dynamic Layout Switching

**Goal:** Switch between multiple layouts at runtime

1. **Create Multiple Layouts**
   - Create 2-3 different layout assets:
     - SemicircularLayout (arc)
     - FlexalonLayoutAdapter with Grid
     - FlexalonLayoutAdapter with Circle

2. **Add Layout Switcher**
   - Select MVI_Controller
   - Add `LayoutSwitcher` component

3. **Configure Switcher**
   - Drag layout assets into "Available Layouts" array
   - Enable "Smooth Transitions"
   - Set "Transition Duration" (e.g., 1 second)

4. **Test Switching**
   - Enter Play Mode
   - Press **Right Arrow** to switch to next layout
   - Press **Left Arrow** to switch to previous layout
   - Or call `NextLayout()` / `PreviousLayout()` from code

5. **Optional: Auto-Switch**
   - Enable "Auto Switch"
   - Set "Auto Switch Interval" (e.g., 5 seconds)
   - Layouts cycle automatically

---

## Common Tasks

### Task: Add Smooth Animations

1. Add `LayoutAnimationController` to MVI_Controller
2. Set Animator Type to **Lerp** or **Curve**
3. Configure speed/curve settings
4. Done - all layout changes now animate smoothly

### Task: Fine-Tune a Single Stem

1. In Edit Mode, select the stem GameObject
2. Use Inspector sliders or Scene View handles
3. See changes in real-time
4. Changes automatically saved to layout

### Task: Reset All Stems to Default

1. Select MVI_Controller
2. Click **"Regenerate Layout (Keep Stems)"**
3. Or for individual stem: Select it → Click "Reset to Default Position"

### Task: Create Multiple Instrument Variants

1. Design first layout as usual
2. Save as Prefab #1
3. Adjust layout parameters
4. Save as Prefab #2
5. Repeat for more variants
6. Instantiate different prefabs as needed

---

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **T** (Scene View) | Position Handle for selected stem |
| **Right Arrow** | Next layout (with LayoutSwitcher) |
| **Left Arrow** | Previous layout (with LayoutSwitcher) |

---

## Inspector Buttons Reference

### ModularSynthController Inspector

| Button | Action |
|--------|--------|
| Generate Stems in Edit Mode | Create stem GameObjects from StemData |
| Clear All Stems | Delete all stem children |
| Regenerate Layout (Keep Stems) | Recalculate positions without destroying stems |
| Add Runtime Editors to All Stems | Attach RuntimeLayoutEditor component |
| Remove Runtime Editors | Clean up RuntimeLayoutEditor components |
| Save as Instrument Prefab | Export configured instrument as prefab |

---

## Tips for Best Results

### Edit Mode Design
✅ Use "Generate Stems in Edit Mode" to preview without play mode  
✅ Live controls update in real-time - no need to regenerate  
✅ Save as prefab once design is complete  

### Runtime Editing
✅ Enable "Smooth Transition" for better visual feedback  
✅ Use "Auto Update" to see changes immediately  
✅ Call SaveToLayout() to persist runtime changes  

### Flexalon Integration
✅ Start with Grid layout - easiest to configure  
✅ Enable animators for smooth transitions  
✅ Experiment with spacing for best visual results  

### Layout Switching
✅ Create distinct layouts (arc vs grid vs circle)  
✅ Enable smooth transitions (1-2 second duration)  
✅ Use animation curves for custom easing  
✅ Combine with LayoutAnimationController for best effect  

---

## Troubleshooting

**Problem:** Stems not appearing  
**Solution:** Check that StemData assets are assigned and layoutStrategy is set

**Problem:** Changes not saving  
**Solution:** Call SaveToLayout() or ensure layout asset isn't read-only

**Problem:** Animations too slow/fast  
**Solution:** Adjust Lerp Speed or Transition Duration settings

**Problem:** Flexalon layouts not working  
**Solution:** Ensure Flexalon package is installed and layout settings are valid

---

## Next Steps

1. ✅ **Read Full Documentation** - See README_LAYOUT_FEATURES.md for complete API reference
2. ✅ **Experiment** - Try different layout combinations
3. ✅ **Create Presets** - Save your favorite configurations as prefabs
4. ✅ **Build UI** - Create runtime controls for users to adjust layouts
5. ✅ **Combine Features** - Use multiple components together for maximum flexibility

---

## Support

For detailed information, see:
- **README_LAYOUT_FEATURES.md** - Comprehensive feature guide
- **IMPLEMENTATION_SUMMARY.md** - Technical implementation details
- Main MVI Documentation
