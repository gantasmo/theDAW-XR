using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using QuestMidiBridge;
using Gantasmo.XRMidi;

namespace Gantasmo.XRMidi.EditorTools
{
    /// <summary>
    /// Builds a 3D, hand-tracked MIDI control surface in the open scene from a
    /// <see cref="GantasmoSurfaceConfig"/> preset: rounded grab faders, round grab
    /// knobs, and round poke buttons, all wired to one <see cref="QuestMidiSender"/>
    /// through a <see cref="MidiControlSurface"/>. Each fader/knob carries a
    /// <see cref="HandGrabInteractable"/> and each button a <see cref="PokeInteractable"/>,
    /// so the Meta Building Blocks comprehensive rig drives them with no extra wiring.
    /// The surface also gets an in-VR layout editor (hamburger + grab handle + Save).
    /// The finished surface floats (no backboard) and is saved as a prefab.
    ///
    /// Geometry is curved: the grabbable/pressable frame is an invisible collider host
    /// and the visible part is a separate cylinder/capsule child, so the round look
    /// never disturbs the interaction axes.
    /// </summary>
    public static class GantasmoControlSurfaceBuilder
    {
        const string AssetRoot = "Assets/QuestMidiBridge";
        const string PrefabDir = AssetRoot + "/Prefabs";
        const string MaterialDir = PrefabDir + "/Materials";
        const string ConfigDir = AssetRoot + "/Config";
        const string DefaultConfigPath = ConfigDir + "/GantasmoSurfaceConfig.asset";

        // ===================================================================
        //  Menu
        // ===================================================================

        [MenuItem("GANTASMO/Control Surface/Build XR MIDI Control Surface", false, 20)]
        public static void Build() => BuildFrom(GetOrCreateDefaultConfig());

        [MenuItem("GANTASMO/Control Surface/Build Surface From Selected Config", false, 21)]
        public static void BuildFromSelected()
        {
            var cfg = Selection.activeObject as GantasmoSurfaceConfig;
            if (cfg == null)
            {
                EditorUtility.DisplayDialog("No config selected",
                    "Select a GantasmoSurfaceConfig asset in the Project window first, " +
                    "or use 'Build XR MIDI Control Surface' to build from the default.", "OK");
                return;
            }
            BuildFrom(cfg);
        }

        [MenuItem("GANTASMO/Control Surface/Create Surface Config Preset", false, 22)]
        public static void CreateConfigPreset()
        {
            EnsureFolders();
            var cfg = ScriptableObject.CreateInstance<GantasmoSurfaceConfig>();
            cfg.ApplyDefaultLayout();
            string path = AssetDatabase.GenerateUniqueAssetPath(ConfigDir + "/GantasmoSurfacePreset.asset");
            AssetDatabase.CreateAsset(cfg, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = cfg;
            EditorGUIUtility.PingObject(cfg);
            Debug.Log($"[GANTASMO] Created surface preset '{path}'. Edit it in the Inspector, then " +
                      "'Build Surface From Selected Config'.");
        }

        [MenuItem("GANTASMO/Control Surface/Reset Surface Config To Default Layout", false, 23)]
        public static void ResetDefaultConfig()
        {
            var cfg = GetOrCreateDefaultConfig();
            cfg.ApplyDefaultLayout();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Selection.activeObject = cfg;
            EditorGUIUtility.PingObject(cfg);
            Debug.Log("[GANTASMO] Reset the default config to the curved DJ layout " +
                      "(6 faders + crossfade, 8 knobs, 12 buttons). Rebuild to apply.");
        }

        // ===================================================================
        //  Build
        // ===================================================================

        public static void BuildFrom(GantasmoSurfaceConfig cfg)
        {
            EnsureFolders();

            Material trackMat = MakeMat("Track", new Color(0.14f, 0.14f, 0.17f));
            Material handleMat = MakeMat("Handle", new Color(0.20f, 0.85f, 0.95f));
            Material knobBaseMat = MakeMat("KnobBase", new Color(0.11f, 0.11f, 0.14f));
            Material knobCapMat = MakeMat("KnobCap", new Color(0.85f, 0.30f, 0.90f));
            Material markMat = MakeMat("KnobMark", Color.white);
            Material buttonMat = MakeMat("Button", new Color(0.95f, 0.65f, 0.15f));
            Material editMat = MakeMat("EditHandle", new Color(0.30f, 0.95f, 0.55f));

            var root = new GameObject("GANTASMO XR MIDI Surface");
            Undo.RegisterCreatedObjectUndo(root, "Build XR MIDI Control Surface");

            var sender = root.AddComponent<QuestMidiSender>();
            var surface = root.AddComponent<MidiControlSurface>();
            surface.sender = sender;
            surface.channel = cfg.channel;
            surface.highResolution = cfg.highResolution;

            // No backboard — the surface floats.

            for (int i = 0; i < cfg.sliders.count; i++)
            {
                var go = BuildSlider(root.transform, ResolvePos(cfg.sliders, i), cfg.sliders.ccStart + i,
                                     cfg.sliders, trackMat, handleMat);
                go.transform.localEulerAngles = ResolveRot(cfg.sliders, i);
            }

            for (int i = 0; i < cfg.crossfade.count; i++)
            {
                var go = BuildSlider(root.transform, ResolvePos(cfg.crossfade, i), cfg.crossfade.ccStart + i,
                                     cfg.crossfade, trackMat, handleMat);
                go.transform.localEulerAngles = ResolveRot(cfg.crossfade, i);
            }

            for (int i = 0; i < cfg.knobs.count; i++)
            {
                var go = BuildKnob(root.transform, ResolvePos(cfg.knobs, i), cfg.knobs.ccStart + i,
                                   cfg.knobs, knobBaseMat, knobCapMat, markMat);
                go.transform.localEulerAngles = ResolveRot(cfg.knobs, i);
            }

            for (int i = 0; i < cfg.buttons.count; i++)
            {
                var go = BuildButton(root.transform, ResolvePos(cfg.buttons, i), cfg.buttons.noteStart + i,
                                     cfg.buttons, buttonMat);
                go.transform.localEulerAngles = ResolveRot(cfg.buttons, i);
            }

            BuildEditSystem(root, cfg, editMat, buttonMat);

            root.transform.position = cfg.position;
            root.transform.eulerAngles = cfg.eulerAngles;

            // Reapply any captured per-part materials (custom shaders) over the generated defaults,
            // before the prefab is saved so they bake into it.
            ApplyMaterialBindings(root.transform, cfg);

            // Fixed path (overwrites) so re-running Build doesn't fork numbered copies.
            string prefabPath = PrefabDir + "/GantasmoXRMidiSurface.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);

            int sliderTotal = cfg.sliders.count + cfg.crossfade.count;
            Debug.Log($"[GANTASMO] Built surface from '{AssetDatabase.GetAssetPath(cfg)}': " +
                      $"{sliderTotal} sliders (CC {cfg.sliders.ccStart}+, crossfade CC {cfg.crossfade.ccStart}), " +
                      $"{cfg.knobs.count} knobs (CC {cfg.knobs.ccStart}+), " +
                      $"{cfg.buttons.count} buttons (Note {cfg.buttons.noteStart}+) on channel {cfg.channel}. " +
                      $"Saved to {prefabPath}. Poke the hamburger in-VR to move/scale and Save the layout.");
        }

        /// <summary>Local position of control <paramref name="i"/>: an explicit override if one
        /// exists, otherwise a centred grid cell (columns across, rows stacking downward).</summary>
        static Vector3 ResolvePos(GantasmoSurfaceConfig.GroupBase g, int i)
        {
            if (g.positionOverrides != null && i < g.positionOverrides.Count)
                return g.positionOverrides[i];

            int cols = Mathf.Max(1, g.columns);
            int col = i % cols;
            int row = i / cols;
            float x = g.origin.x + (col - (cols - 1) * 0.5f) * g.spacing.x;
            float y = g.origin.y - row * g.spacing.y;
            return new Vector3(x, y, g.origin.z);
        }

        /// <summary>Local rotation (euler degrees) of control <paramref name="i"/>: an explicit
        /// override if one exists, otherwise upright.</summary>
        static Vector3 ResolveRot(GantasmoSurfaceConfig.GroupBase g, int i)
        {
            if (g.rotationOverrides != null && i < g.rotationOverrides.Count)
                return g.rotationOverrides[i];
            return Vector3.zero;
        }

        // ---- controls ------------------------------------------------------

        static GameObject BuildSlider(Transform parent, Vector3 localPos, int cc,
                                      GantasmoSurfaceConfig.SliderGroup g, Material trackMat, Material handleMat)
        {
            var root = new GameObject($"Slider_{cc}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            float travel = g.travel;
            GameObject handle;
            if (g.customPrefab != null)
            {
                handle = InstantiateCustom(g.customPrefab, root.transform, g.scale, g.material);
            }
            else
            {
                // Rounded rail: a thin cylinder along the slide axis.
                Shape("Track", root.transform, PrimitiveType.Cylinder, AlongAxis(g.axis, travel * 0.5f),
                    TrackEuler(g.axis), Vector3.Scale(new Vector3(0.014f, (travel + 0.02f) * 0.5f, 0.014f), g.scale),
                    trackMat, false);
                // Handle: an invisible collider host with a rounded cap disc facing the user.
                // Like the knob, the catch volume is larger than the visible cap so a full-hand
                // grab lands without precise targeting. Width/depth stay well inside the ~0.18 m
                // fader pitch (±0.035 m leaves ~0.11 m gap); the modest Y keeps it small against
                // the 0.12 m travel. The OneGrabTranslateTransformer still constrains motion to the
                // rail axis, so a bigger collider only eases the catch, it does not loosen the slide.
                handle = ColliderHost("Handle", root.transform,
                    Vector3.Scale(new Vector3(0.07f, 0.05f, 0.07f), g.scale));
                Shape("Cap", handle.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(90f, 0f, 0f),
                    Vector3.Scale(new Vector3(0.036f, 0.012f, 0.036f), g.scale),
                    g.material != null ? g.material : handleMat, false);
            }

            var rb = AddKinematicBody(handle);

            var move = handle.AddComponent<OneGrabTranslateTransformer>();
            move.InjectOptionalConstraints(TranslateConstraints(g.axis, travel));

            var grab = handle.AddComponent<Grabbable>();
            grab.MaxGrabPoints = 1;
            grab.InjectOptionalOneGrabTransformer(move);
            grab.InjectOptionalRigidbody(rb);
            grab.InjectOptionalThrowWhenUnselected(false);

            var interactable = handle.AddComponent<GrabInteractable>();
            interactable.InjectRigidbody(rb);
            interactable.InjectOptionalPointableElement(grab);

            AddHandGrab(handle, rb, grab);

            var midi = handle.AddComponent<MidiSlider>();
            midi.cc = cc;
            midi.axis = g.axis;
            midi.minLocal = 0f;
            midi.maxLocal = travel;

            // Bipolar (crossfade): rest the handle at the CENTRE of the rail so it reads 0.5
            // (CC 64 neutral) and moves both ways. Unipolar (fade) leaves it at the 0 end.
            if (g.bipolar)
            {
                handle.transform.localPosition = AlongAxis(g.axis, travel * 0.5f);
            }

            return root;
        }

        static GameObject BuildKnob(Transform parent, Vector3 localPos, int cc,
                                    GantasmoSurfaceConfig.KnobGroup g, Material baseMat, Material capMat, Material markMat)
        {
            var root = new GameObject($"Knob_{cc}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            GameObject cap;
            if (g.customPrefab != null)
            {
                cap = InstantiateCustom(g.customPrefab, root.transform, g.scale, g.material);
            }
            else
            {
                // Rounded base ring behind the dial.
                Shape("Base", root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.014f),
                    new Vector3(90f, 0f, 0f), Vector3.Scale(new Vector3(0.062f, 0.008f, 0.062f), g.scale),
                    baseMat, false);
                // Dial: invisible collider host + a round disc + an indicator notch that spins with it.
                // The catch volume is deliberately LARGER than the visible dial so the knob takes a
                // forgiving full-hand grasp instead of needing a precise pinch dead-centre — you can
                // close your whole hand around it, off-centre, and still grab. Sized to stay clear of
                // the ~0.143 m knob pitch (±0.045 m leaves ~0.05 m gap between neighbours). The extra
                // Z depth lets you grab from in front. Rotation still pivots about the dial axis, so a
                // bigger collider changes only how easy it is to catch, not the value mapping.
                cap = ColliderHost("Cap", root.transform,
                    Vector3.Scale(new Vector3(0.09f, 0.09f, 0.05f), g.scale));
                Shape("Dial", cap.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(90f, 0f, 0f),
                    Vector3.Scale(new Vector3(0.05f, 0.012f, 0.05f), g.scale),
                    g.material != null ? g.material : capMat, false);
                Shape("Mark", cap.transform, PrimitiveType.Capsule, new Vector3(0f, 0.016f, 0.014f), Vector3.zero,
                    Vector3.Scale(new Vector3(0.006f, 0.013f, 0.006f), g.scale), markMat, false);
            }

            var rb = AddKinematicBody(cap);

            var rotate = cap.AddComponent<OneGrabRotateTransformer>();
            rotate.InjectOptionalRotationAxis(RotateAxis(g.axis));
            rotate.InjectOptionalConstraints(new OneGrabRotateTransformer.OneGrabRotateConstraints
            {
                MinAngle = new FloatConstraint { Constrain = true, Value = g.minAngle },
                MaxAngle = new FloatConstraint { Constrain = true, Value = g.maxAngle },
            });

            var grab = cap.AddComponent<Grabbable>();
            grab.MaxGrabPoints = 1;
            grab.InjectOptionalOneGrabTransformer(rotate);
            grab.InjectOptionalRigidbody(rb);
            grab.InjectOptionalThrowWhenUnselected(false);

            var interactable = cap.AddComponent<GrabInteractable>();
            interactable.InjectRigidbody(rb);
            interactable.InjectOptionalPointableElement(grab);

            AddHandGrab(cap, rb, grab);

            var midi = cap.AddComponent<MidiKnob>();
            midi.cc = cc;
            midi.axis = g.axis;
            midi.minAngle = g.minAngle;
            midi.maxAngle = g.maxAngle;

            return root;
        }

        static GameObject BuildButton(Transform parent, Vector3 localPos, int note,
                                      GantasmoSurfaceConfig.ButtonGroup g, Material capMat)
        {
            var root = new GameObject($"Button_{note}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            GameObject cap;
            Vector2 face;
            if (g.customPrefab != null)
            {
                cap = InstantiateCustom(g.customPrefab, root.transform, g.scale, g.material);
                face = new Vector2(0.05f * g.scale.x, 0.05f * g.scale.y);
            }
            else
            {
                // Round button: invisible collider host + a disc visual facing the user.
                cap = ColliderHost("Cap", root.transform,
                    Vector3.Scale(new Vector3(0.05f, 0.05f, 0.02f), g.scale));
                Shape("Button", cap.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(90f, 0f, 0f),
                    Vector3.Scale(new Vector3(0.05f, 0.011f, 0.05f), g.scale),
                    g.material != null ? g.material : capMat, false);
                face = new Vector2(0.05f * g.scale.x, 0.05f * g.scale.y);
            }

            MakePoke(cap, face);

            var midi = cap.AddComponent<MidiButton>();
            midi.mode = g.mode;
            midi.note = note;
            midi.velocity = g.velocity;
            // MidiButton auto-finds the PokeInteractable on this object at Awake.

            return root;
        }

        /// <summary>Add the poke surface stack (plane + clipped patch + interactable) to a cap.
        /// The collider host is unscaled, so <paramref name="face"/> is the pressable size in metres.</summary>
        static PokeInteractable MakePoke(GameObject cap, Vector2 face)
        {
            var plane = cap.AddComponent<PlaneSurface>();
            plane.InjectAllPlaneSurface(PlaneSurface.NormalFacing.Forward, false);

            var clipper = cap.AddComponent<BoundsClipper>();
            clipper.Position = Vector3.zero;
            clipper.Size = new Vector3(face.x, face.y, 0.02f);

            var clipped = cap.AddComponent<ClippedPlaneSurface>();
            clipped.InjectAllClippedPlaneSurface(plane, new List<IBoundsClipper> { clipper });

            var poke = cap.AddComponent<PokeInteractable>();
            poke.InjectAllPokeInteractable(clipped);
            return poke;
        }

        /// <summary>
        /// Adds a hand/controller hand-grab interactable to a control handle, wired to
        /// the same Rigidbody and Grabbable its OneGrabTransformer already uses, so the
        /// existing constraint still drives the motion. Required because the Building
        /// Blocks rig only ships HandGrab-family interactors; a bare
        /// <see cref="GrabInteractable"/> is never selected and the handle never moves.
        /// </summary>
        static HandGrabInteractable AddHandGrab(GameObject go, Rigidbody rb, Grabbable grabbable)
        {
            var hg = go.AddComponent<HandGrabInteractable>();
            hg.InjectAllHandGrabInteractable(
                GrabTypeFlags.All, rb,
                GrabbingRule.DefaultPinchRule, GrabbingRule.DefaultPalmRule);
            hg.InjectOptionalPointableElement(grabbable);
            // Full-hand grasp, NOT locked to the control. The SDK default is
            // HandAlignType.AlignOnGrab, which snaps/pins the rendered hand onto the
            // knob or fader the instant you grab it — that is the "lock to the knob"
            // feel. None keeps the hand in its real tracked pose, so you close your
            // whole hand around the control and turn/slide it naturally, with no
            // precise pinch or pose required. Pinch still works too (GrabTypeFlags.All).
            hg.HandAlignment = HandAlignType.None;
            return hg;
        }

        // ---- in-VR layout editor (hamburger + grab handle + Save/Done) -----

        /// <summary>Builds the layout-editor rig: an always-on hamburger that toggles edit
        /// mode, plus a hidden rig (a grab handle to move/rotate/scale the whole surface
        /// and Save/Done buttons). Wired to a <see cref="SurfaceEditMode"/> on the root.</summary>
        static void BuildEditSystem(GameObject root, GantasmoSurfaceConfig cfg, Material handleMat, Material buttonMat)
        {
            var edit = root.AddComponent<SurfaceEditMode>();
            edit.target = root.transform;

            // Hamburger: always visible. Position comes from the config so a hamburger you
            // relocate (and Capture) survives a rebuild instead of snapping back to default.
            GameObject burger = BuildActionButton(root.transform, cfg.hamburgerPosition,
                new Vector3(0.06f, 0.06f, 0.02f), buttonMat, edit, SurfaceEditButton.Action.ToggleEdit);
            Transform burgerCap = burger.transform.Find("Cap");
            for (int i = -1; i <= 1; i++)
                Box($"Bar{i + 1}", burgerCap, new Vector3(0f, i * 0.016f, 0.012f),
                    new Vector3(0.04f, 0.006f, 0.004f), handleMat, false);

            // Edit rig: shown only while editing. Each gizmo does ONE thing so a grab can't
            // accidentally move, rotate and scale at once (the old single free-grab bar was the
            // finnicky "3 at once" control). Move = one-hand translate only; Rotate = one-hand yaw
            // only; Scale = the two-hand free handle (pinch-spread). Laid out around the
            // configurable editRigPosition, with Save / Cancel on the ends.
            var rig = new GameObject("EditRig");
            rig.transform.SetParent(root.transform, false);
            Vector3 b = cfg.editRigPosition;

            BuildActionButton(rig.transform, b + new Vector3(-0.42f, 0f, 0f),
                new Vector3(0.11f, 0.06f, 0.02f), handleMat, edit, SurfaceEditButton.Action.Save);
            BuildMoveHandle(rig.transform, root.transform, b + new Vector3(-0.18f, 0f, 0f), handleMat);
            BuildScaleHandle(rig.transform, root.transform, b, handleMat);
            BuildYawHandle(rig.transform, root.transform, b + new Vector3(0.18f, 0f, 0f), handleMat);
            BuildActionButton(rig.transform, b + new Vector3(0.42f, 0f, 0f),
                new Vector3(0.11f, 0.06f, 0.02f), buttonMat, edit, SurfaceEditButton.Action.Cancel);

            edit.editRig = rig;
            rig.SetActive(false);
        }

        static GameObject BuildActionButton(Transform parent, Vector3 localPos, Vector3 size,
                                            Material mat, SurfaceEditMode editor, SurfaceEditButton.Action action)
        {
            var root = new GameObject($"Edit_{action}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            GameObject cap = ColliderHost("Cap", root.transform, size);
            Shape("Face", cap.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(90f, 0f, 0f),
                new Vector3(size.x, 0.01f, size.y), mat, false);
            MakePoke(cap, new Vector2(size.x, size.y));

            var btn = cap.AddComponent<SurfaceEditButton>();
            btn.editor = editor;
            btn.action = action;
            return root;
        }

        /// <summary>Move-only handle: one-hand free translation retargeted onto the whole surface,
        /// with NO rotation or scale, so a grab only ever slides the surface around.</summary>
        static void BuildMoveHandle(Transform parent, Transform target, Vector3 localPos, Material mat)
        {
            GameObject bar = Shape("Move Handle", parent, PrimitiveType.Sphere, localPos,
                Vector3.zero, new Vector3(0.07f, 0.07f, 0.07f), mat, true);
            var rb = AddKinematicBody(bar);

            var move = bar.AddComponent<OneGrabTranslateTransformer>();
            // All axes unconstrained (free 3-axis translate); FloatConstraint defaults to Constrain=false.
            move.InjectOptionalConstraints(new OneGrabTranslateTransformer.OneGrabTranslateConstraints
            {
                ConstraintsAreRelative = false,
                MinX = new FloatConstraint(), MaxX = new FloatConstraint(),
                MinY = new FloatConstraint(), MaxY = new FloatConstraint(),
                MinZ = new FloatConstraint(), MaxZ = new FloatConstraint(),
            });

            var grab = bar.AddComponent<Grabbable>();
            grab.MaxGrabPoints = 1;
            grab.InjectOptionalTargetTransform(target);
            grab.InjectOptionalOneGrabTransformer(move);
            grab.InjectOptionalRigidbody(rb);
            grab.InjectOptionalThrowWhenUnselected(false);

            AddHandGrab(bar, rb, grab);
        }

        /// <summary>Yaw-only handle: one-hand rotation about world-up retargeted onto the surface,
        /// so you turn it around ONE axis instead of a finnicky free spin.</summary>
        static void BuildYawHandle(Transform parent, Transform target, Vector3 localPos, Material mat)
        {
            GameObject bar = Shape("Rotate Handle (yaw)", parent, PrimitiveType.Capsule, localPos,
                new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.05f, 0.05f), mat, true);
            var rb = AddKinematicBody(bar);

            var rotate = bar.AddComponent<OneGrabRotateTransformer>();
            rotate.InjectOptionalRotationAxis(OneGrabRotateTransformer.Axis.Up);

            var grab = bar.AddComponent<Grabbable>();
            grab.MaxGrabPoints = 1;
            grab.InjectOptionalTargetTransform(target);
            grab.InjectOptionalOneGrabTransformer(rotate);
            grab.InjectOptionalRigidbody(rb);
            grab.InjectOptionalThrowWhenUnselected(false);

            AddHandGrab(bar, rb, grab);
        }

        /// <summary>Scale handle: TWO-hand pinch-spread scales (and rotates) the surface. No one-grab
        /// transformer is injected, so a single hand on it does nothing — scaling never happens by
        /// accident while you are moving with the dedicated move handle.</summary>
        static void BuildScaleHandle(Transform parent, Transform target, Vector3 localPos, Material mat)
        {
            GameObject bar = Shape("Scale Handle (two hands)", parent, PrimitiveType.Sphere, localPos,
                Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f), mat, true);
            var rb = AddKinematicBody(bar);

            var free = bar.AddComponent<GrabFreeTransformer>();

            var grab = bar.AddComponent<Grabbable>();
            grab.MaxGrabPoints = 2;
            grab.InjectOptionalTargetTransform(target);
            grab.InjectOptionalTwoGrabTransformer(free); // two-hand only
            grab.InjectOptionalRigidbody(rb);
            grab.InjectOptionalThrowWhenUnselected(false);

            AddHandGrab(bar, rb, grab);
        }

        // ---- repair (fix already-built surfaces without a full rebuild) -----

        [MenuItem("GANTASMO/Control Surface/Repair XR MIDI Surface Interactions", false, 26)]
        public static void RepairInteractions()
        {
            int total = 0;

            // 1. Repair the surface prefab asset(s) so every instance inherits the fix.
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                int n = RepairUnder(contents);
                if (n > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
                if (n > 0) Debug.Log($"[GANTASMO] Repaired {n} grab control(s) in prefab '{path}'.");
                total += n;
            }

            // 2. Repair any non-prefab surface instances already in the open scene.
            //    Skip prefab instances: step 1 already fixed their source asset and
            //    the instance inherits it, so touching it here would add a duplicate.
            foreach (var surface in Object.FindObjectsByType<MidiControlSurface>(FindObjectsInactive.Include))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(surface.gameObject)) continue;
                int n = RepairUnder(surface.gameObject);
                if (n > 0)
                {
                    EditorUtility.SetDirty(surface.gameObject);
                    EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
                    Debug.Log($"[GANTASMO] Repaired {n} grab control(s) on scene object '{surface.name}'.");
                }
                total += n;
            }

            AssetDatabase.SaveAssets();
            if (total == 0)
                Debug.Log("[GANTASMO] Nothing to repair — every slider/knob already has a free, non-locking hand grab.");
            else
                Debug.Log($"[GANTASMO] Repair complete: fixed {total} grab control(s) " +
                          "(added hand-grab where missing / set the free no-lock grasp). " +
                          "Rebuild the surface too if you want the larger, more forgiving grab volumes.");
        }

        static int RepairUnder(GameObject root)
        {
            int count = 0;
            foreach (var s in root.GetComponentsInChildren<MidiSlider>(true)) count += EnsureHandGrab(s.gameObject);
            foreach (var k in root.GetComponentsInChildren<MidiKnob>(true)) count += EnsureHandGrab(k.gameObject);
            return count;
        }

        static int EnsureHandGrab(GameObject go)
        {
            var existing = go.GetComponent<HandGrabInteractable>();
            if (existing != null)
            {
                // Already has a hand grab — make sure it is the free, non-locking grasp
                // (older surfaces were built with the SDK default AlignOnGrab, which pins
                // the hand to the control). Re-aligning needs no rebuild, so the captured
                // layout is preserved.
                if (existing.HandAlignment != HandAlignType.None)
                {
                    existing.HandAlignment = HandAlignType.None;
                    return 1;
                }
                return 0;
            }
            var rb = go.GetComponent<Rigidbody>();
            var grab = go.GetComponent<Grabbable>();
            if (rb == null || grab == null) return 0; // not a grab control; leave it alone
            AddHandGrab(go, rb, grab);
            return 1;
        }

        // ---- capture (save a hand-arranged surface back into a preset) ------

        [MenuItem("GANTASMO/Control Surface/Capture Surface Layout Into Default Config", false, 24)]
        public static void CaptureIntoDefault()
        {
            var cfg = GetOrCreateDefaultConfig();
            if (!CaptureInto(cfg)) return;
            Selection.activeObject = cfg;
            EditorGUIUtility.PingObject(cfg);
            Debug.Log("[GANTASMO] Saved the current scene arrangement as the DEFAULT layout. " +
                      "Run 'Build XR MIDI Control Surface' to regenerate that exact shape with the " +
                      "current (non-deprecated) components.");
        }

        [MenuItem("GANTASMO/Control Surface/Capture Surface Layout Into Selected Config", false, 25)]
        public static void CaptureLayout()
        {
            var cfg = Selection.activeObject as GantasmoSurfaceConfig;
            if (cfg == null)
            {
                EditorUtility.DisplayDialog("No config selected",
                    "Select the GantasmoSurfaceConfig asset to write into, in the Project window, " +
                    "or use 'Capture Surface Layout Into Default Config'.", "OK");
                return;
            }
            CaptureInto(cfg);
        }

        /// <summary>Read the live surface in the open scene into <paramref name="cfg"/> as
        /// per-control position/rotation overrides + counts + CC/Note starts + the root
        /// transform. Returns false (after a dialog) when no surface is open. A later Build
        /// reproduces the same shape with the current, non-deprecated components.</summary>
        static bool CaptureInto(GantasmoSurfaceConfig cfg)
        {
            var surface = Object.FindAnyObjectByType<MidiControlSurface>();
            if (surface == null)
            {
                EditorUtility.DisplayDialog("No surface in scene",
                    "Open a scene that contains a built surface (a MidiControlSurface) first.", "OK");
                return false;
            }

            Transform root = surface.transform;

            // Sliders split by axis: vertical faders -> sliders group, horizontal -> crossfade.
            var allSliders = surface.GetComponentsInChildren<MidiSlider>(true).OrderBy(s => s.cc).ToList();
            var faders = allSliders.Where(s => s.axis != MidiSlider.SlideAxis.X).ToList();
            var crossfaders = allSliders.Where(s => s.axis == MidiSlider.SlideAxis.X).ToList();

            cfg.sliders.count = faders.Count;
            if (faders.Count > 0) cfg.sliders.ccStart = faders[0].cc;
            cfg.sliders.positionOverrides = faders.Select(s => LocalOf(root, s.transform)).ToList();
            cfg.sliders.rotationOverrides = faders.Select(s => LocalEulerOf(root, s.transform)).ToList();

            cfg.crossfade.count = crossfaders.Count;
            if (crossfaders.Count > 0) cfg.crossfade.ccStart = crossfaders[0].cc;
            cfg.crossfade.axis = MidiSlider.SlideAxis.X;
            cfg.crossfade.positionOverrides = crossfaders.Select(s => LocalOf(root, s.transform)).ToList();
            cfg.crossfade.rotationOverrides = crossfaders.Select(s => LocalEulerOf(root, s.transform)).ToList();

            var knobs = surface.GetComponentsInChildren<MidiKnob>(true).OrderBy(k => k.cc).ToList();
            cfg.knobs.count = knobs.Count;
            if (knobs.Count > 0) cfg.knobs.ccStart = knobs[0].cc;
            cfg.knobs.positionOverrides = knobs.Select(k => LocalOf(root, k.transform)).ToList();
            cfg.knobs.rotationOverrides = knobs.Select(k => LocalEulerOf(root, k.transform)).ToList();

            var buttons = surface.GetComponentsInChildren<MidiButton>(true).OrderBy(b => b.note).ToList();
            cfg.buttons.count = buttons.Count;
            if (buttons.Count > 0) cfg.buttons.noteStart = buttons[0].note;
            cfg.buttons.positionOverrides = buttons.Select(b => LocalOf(root, b.transform)).ToList();
            cfg.buttons.rotationOverrides = buttons.Select(b => LocalEulerOf(root, b.transform)).ToList();

            // Every visible part's material, keyed by its path under the root, so a custom shader on
            // ANY piece (dial, base, mark, rail, button face, edit-rig handle) survives a rebuild.
            cfg.materialBindings = new List<GantasmoSurfaceConfig.MaterialBinding>();
            foreach (var r in surface.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial == null) continue;
                cfg.materialBindings.Add(new GantasmoSurfaceConfig.MaterialBinding
                {
                    path = PathFrom(root, r.transform),
                    material = r.sharedMaterial,
                });
            }

            // Persist the hamburger + edit-rig placement so a relocated menu button / rig
            // survives a rebuild instead of snapping back to the default spot.
            var burgerT = root.Find("Edit_ToggleEdit");
            if (burgerT != null) cfg.hamburgerPosition = root.InverseTransformPoint(burgerT.position);
            var editRigT = root.Find("EditRig");
            if (editRigT != null) cfg.editRigPosition = root.InverseTransformPoint(editRigT.position);

            cfg.position = root.position;
            cfg.eulerAngles = root.eulerAngles;

            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GANTASMO] Captured layout from '{surface.name}' into '{AssetDatabase.GetAssetPath(cfg)}' " +
                      $"({faders.Count} faders, {crossfaders.Count} crossfade, {knobs.Count} knobs, {buttons.Count} buttons, " +
                      $"{cfg.materialBindings.Count} part materials). " +
                      "Overrides now drive the build; clear them to return to the grid.");
            return true;
        }

        /// <summary>Position of a control, in the surface root's local space. The control's
        /// container (the <c>Slider_/Knob_/Button_</c> parent) holds the placement.</summary>
        static Vector3 LocalOf(Transform root, Transform control)
        {
            Transform container = control.parent != null ? control.parent : control;
            return root.InverseTransformPoint(container.position);
        }

        /// <summary>Rotation (euler degrees) of a control's container, in the surface root's
        /// local space.</summary>
        static Vector3 LocalEulerOf(Transform root, Transform control)
        {
            Transform container = control.parent != null ? control.parent : control;
            return (Quaternion.Inverse(root.rotation) * container.rotation).eulerAngles;
        }

        /// <summary>Transform path of <paramref name="t"/> relative to <paramref name="root"/>,
        /// e.g. "Knob_40/Cap/Dial". Empty when t is the root.</summary>
        static string PathFrom(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null && cur != root; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>Reapply every captured per-renderer material onto the freshly built surface by
        /// matching transform paths, so custom shaders on any part survive a rebuild. Unmatched
        /// paths (a removed or renamed control) are skipped, leaving the generated default.</summary>
        static void ApplyMaterialBindings(Transform root, GantasmoSurfaceConfig cfg)
        {
            if (cfg.materialBindings == null) return;
            foreach (var b in cfg.materialBindings)
            {
                if (b == null || b.material == null || string.IsNullOrEmpty(b.path)) continue;
                var t = root.Find(b.path);
                if (t == null) continue;
                var r = t.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = b.material;
            }
        }

        // ---- config --------------------------------------------------------

        static GantasmoSurfaceConfig GetOrCreateDefaultConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GantasmoSurfaceConfig>(DefaultConfigPath);
            if (cfg != null) return cfg;

            EnsureFolders();
            cfg = ScriptableObject.CreateInstance<GantasmoSurfaceConfig>();
            cfg.ApplyDefaultLayout();
            AssetDatabase.CreateAsset(cfg, DefaultConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GANTASMO] Created default surface config at '{DefaultConfigPath}'. " +
                      "Edit it (counts, position, scale, materials, custom prefabs) and rebuild.");
            return cfg;
        }

        // ---- axis helpers --------------------------------------------------

        static Vector3 AlongAxis(MidiSlider.SlideAxis axis, float v) =>
            axis == MidiSlider.SlideAxis.X ? new Vector3(v, 0, 0) :
            axis == MidiSlider.SlideAxis.Y ? new Vector3(0, v, 0) : new Vector3(0, 0, v);

        /// <summary>Euler rotation that orients a default (Y-axis) cylinder rail along the
        /// slide axis.</summary>
        static Vector3 TrackEuler(MidiSlider.SlideAxis axis) =>
            axis == MidiSlider.SlideAxis.X ? new Vector3(0f, 0f, 90f) :
            axis == MidiSlider.SlideAxis.Z ? new Vector3(90f, 0f, 0f) : Vector3.zero;

        static OneGrabTranslateTransformer.OneGrabTranslateConstraints TranslateConstraints(
            MidiSlider.SlideAxis axis, float travel)
        {
            FloatConstraint Z() => new FloatConstraint { Constrain = true, Value = 0f };
            var c = new OneGrabTranslateTransformer.OneGrabTranslateConstraints
            {
                ConstraintsAreRelative = false,
                MinX = Z(), MaxX = Z(), MinY = Z(), MaxY = Z(), MinZ = Z(), MaxZ = Z(),
            };
            var hi = new FloatConstraint { Constrain = true, Value = travel };
            if (axis == MidiSlider.SlideAxis.X) c.MaxX = hi;
            else if (axis == MidiSlider.SlideAxis.Y) c.MaxY = hi;
            else c.MaxZ = hi;
            return c;
        }

        static OneGrabRotateTransformer.Axis RotateAxis(MidiKnob.KnobAxis axis) =>
            axis == MidiKnob.KnobAxis.X ? OneGrabRotateTransformer.Axis.Right :
            axis == MidiKnob.KnobAxis.Y ? OneGrabRotateTransformer.Axis.Up :
                                          OneGrabRotateTransformer.Axis.Forward;

        // ---- primitive / prefab helpers ------------------------------------

        static Rigidbody AddKinematicBody(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            return rb;
        }

        /// <summary>An empty GameObject with only a BoxCollider — the interaction frame for a
        /// control, kept separate from the curved visual so the round look never disturbs the
        /// grab/poke axes.</summary>
        static GameObject ColliderHost(string name, Transform parent, Vector3 colliderSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var bc = go.AddComponent<BoxCollider>();
            bc.size = colliderSize;
            return go;
        }

        /// <summary>Instantiate a user-supplied 3D object as a control part: a plain
        /// (unpacked) copy, scaled, materialed, and guaranteed to have a collider.</summary>
        static GameObject InstantiateCustom(GameObject prefab, Transform parent, Vector3 scale, Material mat)
        {
            var go = (GameObject)Object.Instantiate(prefab, parent);
            go.name = prefab.name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;

            if (mat != null)
            {
                var r = go.GetComponentInChildren<Renderer>();
                if (r != null) r.sharedMaterial = mat;
            }
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
            return go;
        }

        /// <summary>Create a primitive (cube/cylinder/capsule/sphere) child. Curved primitives
        /// give the controls their rounded look; pass withCollider=false for pure visuals.</summary>
        static GameObject Shape(string name, Transform parent, PrimitiveType type, Vector3 localPos,
                                Vector3 localEuler, Vector3 localScale, Material mat, bool withCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!withCollider)
            {
                Collider col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos,
                              Vector3 localScale, Material mat, bool withCollider) =>
            Shape(name, parent, PrimitiveType.Cube, localPos, Vector3.zero, localScale, mat, withCollider);

        // Load-or-create the named default material at a FIXED path so every rebuild REUSES the
        // same asset instead of spawning "Name 1.mat", "Name 2.mat"... each time. There are only a
        // handful of these (Track/Handle/KnobBase/KnobCap/KnobMark/Button/EditHandle) and they are
        // shared across every control for batching. An existing one is reused as-is so a tweak to a
        // default survives; the colour is set only on first creation. Custom per-part materials come
        // from the config's materialBindings, not from here.
        static Material MakeMat(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder(AssetRoot, "Prefabs");
            if (!AssetDatabase.IsValidFolder(MaterialDir))
                AssetDatabase.CreateFolder(PrefabDir, "Materials");
            if (!AssetDatabase.IsValidFolder(ConfigDir))
                AssetDatabase.CreateFolder(AssetRoot, "Config");
        }
    }
}
