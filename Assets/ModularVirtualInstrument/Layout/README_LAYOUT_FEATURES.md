# MVI Layout System - Advanced Features Guide

## Overview

This guide covers the advanced layout features for the Modular Virtual Instrument (MVI) system, including runtime layout editing, Flexalon integration, and dynamic layout switching.

## Table of Contents

1. [Runtime Layout Editing](#runtime-layout-editing)
2. [Flexalon Integration](#flexalon-integration)
3. [Layout Animation](#layout-animation)
4. [Dynamic Layout Switching](#dynamic-layout-switching)
5. [Editor Workflow](#editor-workflow)
6. [API Reference](#api-reference)

---

## Runtime Layout Editing

### RuntimeLayoutEditor Component

The `RuntimeLayoutEditor` component allows you to edit individual stem positions at runtime or in edit mode.

#### Setup

1. Generate stems using the ModularSynthController editor
2. Click "Add Runtime Editors to All Stems" button in the inspector
3. Select individual stems to edit their position properties

#### Properties

- **Perimeter Position** (0-1): Position along the arc (0 = start, 1 = end)
- **Height Adjustment** (-2 to 2): Vertical offset from base height
- **Depth Offset** (-2 to 2): Radial offset (positive = outward)
- **Additional Rotation** (-180 to 180): Y-axis rotation in degrees

#### Runtime Options

- **Auto Update**: Automatically update position when values change
- **Smooth Transition**: Interpolate smoothly to new positions
- **Transition Speed** (1-20): Speed of smooth transitions

#### Usage Example

```csharp
// Get the runtime editor component
RuntimeLayoutEditor editor = stem.GetComponent<RuntimeLayoutEditor>();

// Adjust position
editor.perimeterPosition = 0.75f;
editor.heightAdjustment = 0.5f;
editor.UpdatePosition();

// Reset to default
editor.ResetToDefault();

// Load from layout
editor.LoadFromLayout();

// Save back to layout
editor.SaveToLayout();
```

---

## Flexalon Integration

### FlexalonLayoutAdapter

The `FlexalonLayoutAdapter` allows you to use Flexalon's powerful layout system as a StemLayoutStrategy.

#### Supported Layout Types

1. **Grid Layout** - Fixed grid with rows and columns
2. **Flexible Layout** - Flexible box layout with wrapping
3. **Circle Layout** - Arrange stems in a circle
4. **Align Layout** - Align stems to parent bounds

#### Setup

1. Create a FlexalonLayoutAdapter asset:
   - Right-click in Project → Create → Modular Virtual Instrument → Layout → Flexalon Adapter
2. Configure layout type and properties
3. Assign to ModularSynthController's Layout Strategy field

#### Grid Layout Properties

- **Columns**: Number of columns in grid
- **Rows**: Number of rows in grid
- **Column Spacing**: Space between columns
- **Row Spacing**: Space between rows

#### Flexible Layout Properties

- **Flex Direction**: Direction for laying out items
- **Gap**: Space between items
- **Wrap**: Enable wrapping to next line

#### Circle Layout Properties

- **Circle Radius**: Radius of the circle
- **Start Angle**: Starting angle in degrees
- **Circle Spacing**: Angular spacing between items

#### Animation Settings

- **Use Animators**: Enable Flexalon animators
- **Animator Type**: Instant, Lerp, or Curve
- **Interpolation Speed**: Speed for lerp animator

#### Usage Example

```csharp
// Create and configure adapter
FlexalonLayoutAdapter adapter = ScriptableObject.CreateInstance<FlexalonLayoutAdapter>();
adapter.layoutType = FlexalonLayoutAdapter.FlexalonLayoutType.Grid;
adapter.columns = 3;
adapter.rows = 2;
adapter.columnSpacing = 1.0f;
adapter.rowSpacing = 1.0f;

// Assign to controller
controller.layoutStrategy = adapter;
controller.RegenerateLayout();

// Apply animator to a stem
adapter.ApplyAnimator(stemGameObject);
```

---

## Layout Animation

### LayoutAnimationController Component

The `LayoutAnimationController` adds smooth animations to layout changes using Flexalon's animation system.

#### Setup

1. Add the `LayoutAnimationController` component to your ModularSynthController GameObject
2. Configure animation settings
3. Animations will automatically apply when layouts change

#### Animator Types

##### None
- Instant position changes, no animation

##### Lerp Animator
- Continuous linear interpolation
- Best for constantly changing layouts
- Properties:
  - **Lerp Speed** (1-20): Interpolation speed
  - **Animate in World Space**: Use world space coordinates

##### Curve Animator
- Animation curve-based transitions
- Restarts each time layout changes
- Properties:
  - **Animation Curve**: Custom easing curve
  - **Curve Duration** (0.1-5): Animation duration

#### Animation Channels

- **Animate Position**: Enable position animation
- **Animate Rotation**: Enable rotation animation
- **Animate Scale**: Enable scale animation

#### Usage Example

```csharp
// Get animation controller
LayoutAnimationController animController = GetComponent<LayoutAnimationController>();

// Configure animations
animController.animatorType = LayoutAnimationController.AnimatorType.Lerp;
animController.lerpSpeed = 5f;
animController.enableAnimations = true;

// Setup animators on all stems
animController.SetupAnimators();

// Temporarily disable animations
animController.DisableAnimations();

// Re-enable animations
animController.EnableAnimations();
```

---

## Dynamic Layout Switching

### LayoutSwitcher Component

The `LayoutSwitcher` allows switching between multiple layout strategies at runtime with smooth transitions.

#### Setup

1. Add `LayoutSwitcher` component to ModularSynthController GameObject
2. Assign multiple layout strategies to the "Available Layouts" array
3. Configure transition settings
4. (Optional) Enable auto-switching or keyboard controls

#### Properties

##### Available Layouts
Array of StemLayoutStrategy assets to switch between

##### Switching Options
- **Smooth Transitions**: Enable animated transitions
- **Transition Duration** (0.1-5): Length of transition
- **Transition Curve**: Easing curve for transitions

##### Auto-Switch Settings
- **Auto Switch**: Automatically cycle through layouts
- **Auto Switch Interval** (1-60): Seconds between switches

##### Input Bindings
- **Next Layout Key**: Key to switch to next layout (default: Right Arrow)
- **Previous Layout Key**: Key to switch to previous layout (default: Left Arrow)
- **Enable Keyboard Input**: Enable keyboard controls

#### Usage Example

```csharp
// Get layout switcher
LayoutSwitcher switcher = GetComponent<LayoutSwitcher>();

// Switch to next layout
switcher.NextLayout();

// Switch to previous layout
switcher.PreviousLayout();

// Switch to specific index
switcher.SwitchToLayout(2);

// Switch by name
switcher.SwitchToLayoutByName("Grid Layout");

// Add new layout at runtime
switcher.AddLayout(newLayoutStrategy);

// Get layout names
string[] layoutNames = switcher.GetLayoutNames();

// Access current layout
StemLayoutStrategy current = switcher.CurrentLayout;
int currentIndex = switcher.CurrentLayoutIndex;
```

---

## Editor Workflow

### Enhanced ModularSynthController Inspector

The enhanced editor provides powerful tools for layout design:

#### Edit Mode Layout Tools

1. **Generate Stems in Edit Mode**
   - Creates stem GameObjects based on StemData assets
   - Applies layout strategy positioning
   - Allows prefab creation

2. **Clear All Stems**
   - Removes all generated stem GameObjects
   - Useful for starting over

3. **Regenerate Layout (Keep Stems)**
   - Recalculates positions without destroying stems
   - Preserves stem components and settings

#### Live Layout Controls

When a SemicircularLayout is assigned and stems exist:

- **Radius Slider**: Adjust semicircle radius (0.5-5)
- **Height Offset Slider**: Adjust base height (-2 to 2)
- **Arc Rotation Slider**: Rotate entire arc (0-360°)
- **Arc Angle Slider**: Adjust arc span (30-360°)
- **Face Center Toggle**: Orient stems toward center

Changes apply immediately and update stem positions in real-time.

#### Per-Stem Controls

Select a stem from the dropdown to edit individual position:

- Perimeter position along arc
- Height adjustment
- Depth offset (radial)
- Additional rotation
- **Reset to Default Position** button

#### Runtime Editors

- **Add Runtime Editors to All Stems**: Attach RuntimeLayoutEditor to each stem
- **Remove Runtime Editors**: Clean up RuntimeLayoutEditor components

#### Scene View Gizmos

With a stem selected:
- **Position Handle (T)**: Move stem freely
- **Custom Sliders**: Fine-tune height and depth
- **Info Box**: Shows current stem parameters
- **Arc Visualization**: Displays layout bounds and center

### StemPositionControl Property Drawer

The custom property drawer provides a clean UI for editing stem positions:

- Labeled sliders for each property
- Tooltips with descriptions
- Visual grouping in inspector
- Easy to read and adjust

---

## API Reference

### RuntimeLayoutEditor

```csharp
public class RuntimeLayoutEditor : MonoBehaviour
{
    // Configuration
    public ModularSynthController controller;
    public int stemIndex;
    
    // Position controls
    public float perimeterPosition;      // 0-1
    public float heightAdjustment;       // -2 to 2
    public float depthOffset;            // -2 to 2
    public float additionalRotation;     // -180 to 180
    
    // Options
    public bool autoUpdate;
    public bool smoothTransition;
    public float transitionSpeed;        // 1-20
    
    // Methods
    public void LoadFromLayout();
    public void SaveToLayout();
    public void UpdatePosition();
    public void ResetToDefault();
}
```

### FlexalonLayoutAdapter

```csharp
public class FlexalonLayoutAdapter : StemLayoutStrategy
{
    // Layout type
    public FlexalonLayoutType layoutType;
    
    // Grid settings
    public int columns;
    public int rows;
    public float columnSpacing;
    public float rowSpacing;
    
    // Flexible settings
    public FlexalonDirection flexDirection;
    public float gap;
    public bool wrap;
    
    // Circle settings
    public float circleRadius;
    public float startAngle;
    public float circleSpacing;
    
    // Animation
    public bool useAnimators;
    public FlexalonAnimatorType animatorType;
    public float interpolationSpeed;
    
    // Methods
    public void ApplyAnimator(GameObject stem);
}
```

### LayoutAnimationController

```csharp
public class LayoutAnimationController : MonoBehaviour
{
    // Settings
    public AnimatorType animatorType;
    public bool enableAnimations;
    
    // Lerp settings
    public float lerpSpeed;              // 1-20
    public bool animateInWorldSpace;
    
    // Curve settings
    public AnimationCurve animationCurve;
    public float curveDuration;          // 0.1-5
    
    // Channels
    public bool animatePosition;
    public bool animateRotation;
    public bool animateScale;
    
    // Methods
    public void SetupAnimators();
    public void CleanupAnimators();
    public void UpdateAnimatorSettings();
    public void DisableAnimations();
    public void EnableAnimations();
    public void ApplyAnimatorToStem(Transform stem);
}
```

### LayoutSwitcher

```csharp
public class LayoutSwitcher : MonoBehaviour
{
    // Layouts
    public StemLayoutStrategy[] availableLayouts;
    public int CurrentLayoutIndex { get; set; }
    public StemLayoutStrategy CurrentLayout { get; }
    
    // Transitions
    public bool smoothTransitions;
    public float transitionDuration;     // 0.1-5
    public AnimationCurve transitionCurve;
    
    // Auto-switch
    public bool autoSwitch;
    public float autoSwitchInterval;     // 1-60
    
    // Input
    public KeyCode nextLayoutKey;
    public KeyCode previousLayoutKey;
    public bool enableKeyboardInput;
    
    // Methods
    public void NextLayout();
    public void PreviousLayout();
    public void SwitchToLayout(int index);
    public void SwitchToLayoutByName(string layoutName);
    public void AddLayout(StemLayoutStrategy layout);
    public void RemoveLayout(int index);
    public string[] GetLayoutNames();
}
```

---

## Tips and Best Practices

### Runtime Editing

1. **Use RuntimeLayoutEditor** for individual stem adjustments during gameplay
2. **Enable smooth transitions** for better visual feedback
3. **Save to layout** to persist changes to the ScriptableObject

### Flexalon Integration

1. **Start with Grid layout** for predictable arrangements
2. **Use Circle layout** for radial instrument configurations
3. **Enable animators** for smooth transitions when using Flexalon
4. **Experiment with spacing** to find the right feel for your instrument

### Layout Animation

1. **Use Lerp animators** for constantly changing layouts
2. **Use Curve animators** for one-time transitions with custom easing
3. **Disable animations** temporarily for instant positioning
4. **Animate in local space** if the parent controller is moving

### Dynamic Switching

1. **Create multiple layout presets** as ScriptableObject assets
2. **Enable smooth transitions** for polished layout changes
3. **Use auto-switch** for demos or visual interest
4. **Provide keyboard controls** for testing different layouts
5. **Combine with LayoutAnimationController** for best results

### Editor Workflow

1. **Generate stems in edit mode** to preview layouts without entering play mode
2. **Use live layout controls** to fine-tune arc parameters
3. **Select individual stems** for precise positioning
4. **Save as prefab** once you're happy with the configuration
5. **Use runtime editors** for stems that need in-scene adjustment

---

## Troubleshooting

### Stems not moving smoothly
- Check that LayoutAnimationController is enabled
- Verify animator type is set to Lerp or Curve
- Increase interpolation speed

### Flexalon layouts not working
- Ensure Flexalon package is imported
- Check that FlexalonLayoutAdapter has valid settings
- Verify columns/rows are sufficient for stem count

### Layout switching too fast/slow
- Adjust `transitionDuration` on LayoutSwitcher
- Modify `transitionCurve` for different easing
- Disable `smoothTransitions` for instant switching

### Runtime editors not saving
- Call `SaveToLayout()` explicitly
- Ensure the SemicircularLayout asset is not read-only
- Check that `autoUpdate` is enabled

---

## Examples

### Example 1: Dynamic Performance Layout

```csharp
// Setup multiple layouts for different performance modes
public class PerformanceLayoutManager : MonoBehaviour
{
    public LayoutSwitcher switcher;
    
    void Start()
    {
        // Start with default layout
        switcher.SwitchToLayout(0);
    }
    
    public void OnPerformanceIntensityChanged(float intensity)
    {
        // Switch to more spread out layout for intense performances
        if (intensity > 0.7f)
        {
            switcher.SwitchToLayoutByName("Wide Grid");
        }
        else
        {
            switcher.SwitchToLayoutByName("Semicircle");
        }
    }
}
```

### Example 2: Interactive Stem Positioning

```csharp
// Allow user to drag stems in VR/XR
public class InteractiveStemEditor : MonoBehaviour
{
    public RuntimeLayoutEditor editor;
    
    void OnDrag(Vector3 worldPosition)
    {
        // Convert world position to perimeter position
        Vector3 toStem = worldPosition - editor.controller.layoutCenter;
        float angle = Mathf.Atan2(toStem.x, toStem.z) * Mathf.Rad2Deg;
        
        // Normalize to 0-1 range based on arc
        SemicircularLayout layout = editor.controller.layoutStrategy as SemicircularLayout;
        float normalized = (angle + layout.arcAngle * 0.5f) / layout.arcAngle;
        
        editor.perimeterPosition = Mathf.Clamp01(normalized);
        editor.UpdatePosition();
    }
    
    void OnDragEnd()
    {
        // Save changes
        editor.SaveToLayout();
    }
}
```

### Example 3: Animated Layout Sequence

```csharp
// Cycle through layouts with custom timing
public class LayoutSequencer : MonoBehaviour
{
    public LayoutSwitcher switcher;
    public float[] layoutDurations;
    
    IEnumerator Start()
    {
        for (int i = 0; i < switcher.availableLayouts.Length; i++)
        {
            switcher.SwitchToLayout(i);
            
            float duration = i < layoutDurations.Length ? layoutDurations[i] : 5f;
            yield return new WaitForSeconds(duration);
        }
    }
}
```

---

## Version History

### v1.0.0
- Initial release of advanced layout features
- RuntimeLayoutEditor component
- FlexalonLayoutAdapter integration
- LayoutAnimationController with Flexalon support
- LayoutSwitcher for dynamic layout changes
- Enhanced ModularSynthController editor
- StemPositionControl property drawer

---

## Support

For issues, questions, or feature requests related to the MVI Layout System, please refer to the main MVI documentation or contact support.
