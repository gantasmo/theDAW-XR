using UnityEditor;
using UnityEngine;
using QuestMidiBridge;

namespace Gantasmo.EditorTools
{
    /// <summary>One-click setup for the MIDI Reactor reactive chrome. Adds the component
    /// to the scene, mounts it to the main camera, and wires the QuestMidiSender.</summary>
    public static class GantasmoVisorSetup
    {
        [MenuItem("GANTASMO/MIDI Reactor/Add To Scene", priority = 80)]
        public static void AddReactor()
        {
            var existing = Object.FindAnyObjectByType<GantasmoVisor>();
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                if (!EditorUtility.DisplayDialog(
                        "MIDI Reactor",
                        "A MIDI Reactor is already in the scene. Add another?",
                        "Add another", "Cancel"))
                    return;
            }

            var go = new GameObject("GANTASMO MIDI Reactor");
            var reactor = go.AddComponent<GantasmoVisor>();

            // Mount to the headset camera if one is present.
            var cam = Camera.main;
            if (cam != null)
            {
                reactor.mountTarget = cam.transform;
                go.transform.SetParent(cam.transform, worldPositionStays: false);
            }

            // Wire the MIDI return circuit if a sender exists.
            var sender = Object.FindAnyObjectByType<QuestMidiSender>();
            if (sender != null) reactor.sender = sender;

            Undo.RegisterCreatedObjectUndo(go, "Add MIDI Reactor");
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);

            if (sender == null)
            {
                EditorUtility.DisplayDialog(
                    "MIDI Reactor added",
                    "Added the MIDI Reactor to the scene.\n\nNo QuestMidiSender was found. " +
                    "Add one (GANTASMO > MIDI Bridge > Setup Wizard) so it can react to the " +
                    "DAW's return MIDI on the \"QuestMIDI-Return\" port.",
                    "OK");
            }
        }
    }
}
