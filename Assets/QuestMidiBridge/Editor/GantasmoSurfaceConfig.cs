using System.Collections.Generic;
using UnityEngine;

namespace Gantasmo.XRMidi.EditorTools
{
    /// <summary>
    /// A saved layout/preset for the GANTASMO XR MIDI surface. Edit it in the
    /// Inspector to change the count, position, rotation, scale, materials, and
    /// (optionally) the custom 3D object used for each control group, then build from
    /// it with <c>GANTASMO ▸ Build Surface From Selected Config</c>. Duplicate the
    /// asset to keep several presets; mark one as the project default (see the builder).
    ///
    /// This is an editor-only authoring asset — it is never loaded at runtime and
    /// never ships in a build. (Runtime layout edits are saved separately by
    /// <see cref="Gantasmo.XRMidi.SurfaceLayoutStore"/>.)
    /// </summary>
    [CreateAssetMenu(menuName = "GANTASMO/Surface Config (Preset)", fileName = "GantasmoSurfaceConfig")]
    public class GantasmoSurfaceConfig : ScriptableObject
    {
        [Header("Surface root (world transform)")]
        [Tooltip("Where the surface root sits. Default floats it in front of a standing player.")]
        public Vector3 position = new Vector3(0f, 1.05f, 0.4f);
        public Vector3 eulerAngles = Vector3.zero;

        [Header("Edit rig placement")]
        [Tooltip("Local position of the always-on hamburger (edit-mode toggle) button. Capture writes the " +
                 "moved position back here so a hamburger you relocated survives a rebuild.")]
        public Vector3 hamburgerPosition = new Vector3(0.56f, 0.60f, 0f);
        [Tooltip("Local position of the in-VR edit rig (the move/rotate handles + Save/Cancel buttons).")]
        public Vector3 editRigPosition = new Vector3(0f, 0.74f, 0f);

        [Header("MIDI")]
        [Range(1, 16)] public int channel = 1;
        [Tooltip("Send 14-bit CC for sliders/knobs (consumes cc and cc+32).")]
        public bool highResolution = false;

        [Header("Control groups")]
        public SliderGroup sliders = new SliderGroup();
        // Defaults to 0 so an older config asset (saved before this field existed) does
        // not spawn stray crossfaders on rebuild. ApplyDefaultLayout sets it to 1.
        public SliderGroup crossfade = new SliderGroup { count = 0 };
        public KnobGroup knobs = new KnobGroup();
        public ButtonGroup buttons = new ButtonGroup();

        [Header("Per-renderer materials (captured)")]
        [Tooltip("Material for every visible part of the surface, captured by path from the live scene: " +
                 "dial faces, knob bases and marks, fader rails, button faces, and the edit-rig handles. " +
                 "'Capture Surface Layout' fills this; a rebuild reapplies each by path so a custom shader " +
                 "on any part survives. Leave empty to use the generated defaults.")]
        public List<MaterialBinding> materialBindings = new List<MaterialBinding>();

        /// <summary>A captured material keyed by the renderer's transform path under the surface root.</summary>
        [System.Serializable]
        public class MaterialBinding
        {
            [Tooltip("Transform path of the renderer relative to the surface root, e.g. 'Knob_40/Cap/Dial'.")]
            public string path;
            public Material material;
        }

        /// <summary>Layout + appearance shared by every control group.</summary>
        [System.Serializable]
        public class GroupBase
        {
            [Tooltip("How many controls in this group.")]
            public int count = 8;

            [Tooltip("Controls per row. Rows stack downward (−Y). Set to count for a single row.")]
            public int columns = 8;

            [Tooltip("X = horizontal pitch between columns, Y = vertical pitch between rows (metres).")]
            public Vector2 spacing = new Vector2(0.10f, 0.13f);

            [Tooltip("Local offset of this group within the surface root. Columns are centred on origin.x.")]
            public Vector3 origin = Vector3.zero;

            [Tooltip("Per-control scale multiplier applied to the generated mesh (or the custom prefab).")]
            public Vector3 scale = Vector3.one;

            [Tooltip("Material for the grabbable/pressable part. Leave empty to use the generated default.")]
            public Material material;

            [Tooltip("Optional custom 3D object used instead of the generated primitive. " +
                     "Its instantiated root receives the interactable + MIDI components and a " +
                     "BoxCollider if it has none.")]
            public GameObject customPrefab;

            [Tooltip("Explicit local positions (relative to the surface root). When set, an entry " +
                     "overrides the grid for that control index; leave empty to use the grid. " +
                     "'Capture Surface Layout' fills this from a surface you arranged by hand.")]
            public List<Vector3> positionOverrides = new List<Vector3>();

            [Tooltip("Explicit local rotations (euler degrees) per control index. When set, an " +
                     "entry rotates that control; leave empty for upright. Used by the arc layout " +
                     "to tilt the outer buttons.")]
            public List<Vector3> rotationOverrides = new List<Vector3>();
        }

        [System.Serializable]
        public class SliderGroup : GroupBase
        {
            [Tooltip("First CC number; subsequent sliders increment from here.")]
            public int ccStart = 1;
            [Tooltip("Rail length the handle travels along (metres).")]
            public float travel = 0.12f;
            public MidiSlider.SlideAxis axis = MidiSlider.SlideAxis.Y;
            [Tooltip("Bipolar (-1 .. 0 .. +1) crossfade: the handle rests at the CENTRE of the rail (CC 64 " +
                     "neutral) and moves both ways. Off = unipolar (0 .. 1) fade resting at one end. Default off.")]
            public bool bipolar = false;
        }

        [System.Serializable]
        public class KnobGroup : GroupBase
        {
            [Tooltip("First CC number; subsequent knobs increment from here.")]
            public int ccStart = 40;
            public float minAngle = -135f;
            public float maxAngle = 135f;
            public MidiKnob.KnobAxis axis = MidiKnob.KnobAxis.Z;
        }

        [System.Serializable]
        public class ButtonGroup : GroupBase
        {
            [Tooltip("First Note number; subsequent buttons increment from here.")]
            public int noteStart = 36;
            public MidiButton.Mode mode = MidiButton.Mode.Note;
            [Range(1, 127)] public int velocity = 110;
        }

        /// <summary>
        /// Fills a fresh asset with the curved DJ-fan default: six faders on an upward
        /// arc, one horizontal crossfade, eight knobs on an arc, and twelve buttons in a
        /// tilted upper row plus a flat lower row. The placement is written as
        /// position/rotation overrides so it survives a rebuild and round-trips through
        /// Capture. Called by the builder when it auto-creates the default.
        /// </summary>
        public void ApplyDefaultLayout()
        {
            position = new Vector3(0f, 1.05f, 0.4f);
            // Faces the player and tilts back like a desk: 180° Y to turn the control
            // faces toward the user, −45° X to lay it back to a console angle.
            eulerAngles = new Vector3(-45f, 180f, 0f);
            hamburgerPosition = new Vector3(0.56f, 0.60f, 0f);
            editRigPosition = new Vector3(0f, 0.74f, 0f);
            channel = 1;
            highResolution = false;

            // 6 vertical faders, centre raised (∩ arc).
            sliders = new SliderGroup
            {
                count = 6, columns = 6, ccStart = 1, travel = 0.12f, axis = MidiSlider.SlideAxis.Y,
                positionOverrides = new List<Vector3>(),
            };
            float[] sliderX = { -0.45f, -0.27f, -0.09f, 0.09f, 0.27f, 0.45f };
            foreach (float x in sliderX)
            {
                float t = 1f - Mathf.Pow(x / 0.45f, 2f);
                sliders.positionOverrides.Add(new Vector3(x, 0.40f + 0.14f * t, 0f));
            }

            // 1 horizontal crossfade at the bottom centre. Bipolar: rests at centre (CC 64).
            crossfade = new SliderGroup
            {
                count = 1, columns = 1, ccStart = 7, travel = 0.30f, axis = MidiSlider.SlideAxis.X,
                bipolar = true,
                positionOverrides = new List<Vector3> { new Vector3(0f, -0.34f, 0f) },
            };

            // 8 knobs on the same upward arc, wider than the faders.
            knobs = new KnobGroup
            {
                count = 8, columns = 8, ccStart = 40, minAngle = -135f, maxAngle = 135f,
                axis = MidiKnob.KnobAxis.Z, positionOverrides = new List<Vector3>(),
            };
            for (int i = 0; i < 8; i++)
            {
                float x = Mathf.Lerp(-0.50f, 0.50f, i / 7f);
                float t = 1f - Mathf.Pow(x / 0.50f, 2f);
                knobs.positionOverrides.Add(new Vector3(x, 0.06f + 0.17f * t, 0f));
            }

            // 12 buttons: an upper row of 6 (tilted to follow the arc) over a flat row of 6.
            buttons = new ButtonGroup
            {
                count = 12, columns = 6, noteStart = 36, mode = MidiButton.Mode.Note, velocity = 110,
                positionOverrides = new List<Vector3>(), rotationOverrides = new List<Vector3>(),
            };
            float[] buttonX = { -0.40f, -0.24f, -0.08f, 0.08f, 0.24f, 0.40f };
            foreach (float x in buttonX) // upper row: arc + tilt
            {
                float t = 1f - Mathf.Pow(x / 0.40f, 2f);
                buttons.positionOverrides.Add(new Vector3(x, -0.04f + 0.03f * t, 0f));
                buttons.rotationOverrides.Add(new Vector3(0f, 0f, -40f * x));
            }
            foreach (float x in buttonX) // lower row: flat
            {
                buttons.positionOverrides.Add(new Vector3(x, -0.18f, 0f));
                buttons.rotationOverrides.Add(Vector3.zero);
            }
        }
    }
}
