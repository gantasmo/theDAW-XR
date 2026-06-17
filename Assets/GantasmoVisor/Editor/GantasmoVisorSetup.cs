using UnityEditor;
using UnityEngine;
using QuestMidiBridge;

namespace Gantasmo.EditorTools
{
    /// <summary>One-click setup for the GANTASMO Visor — adds the component to the
    /// scene, mounts it to the main camera, and wires the QuestMidiSender.</summary>
    public static class GantasmoVisorSetup
    {
        [MenuItem("GANTASMO/Add Visor (Chrome XXL 8008135)", priority = 0)]
        public static void AddVisor()
        {
            var existing = Object.FindAnyObjectByType<GantasmoVisor>();
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                if (!EditorUtility.DisplayDialog(
                        "GANTASMO Visor",
                        "A GANTASMO Visor is already in the scene. Add another?",
                        "Add another", "Cancel"))
                    return;
            }

            var go = new GameObject("GANTASMO Visor (Chrome XXL 8008135)");
            var visor = go.AddComponent<GantasmoVisor>();

            // Mount to the headset camera if we can find one.
            var cam = Camera.main;
            if (cam != null)
            {
                visor.mountTarget = cam.transform;
                go.transform.SetParent(cam.transform, worldPositionStays: false);
            }

            // Wire the MIDI return circuit if a sender exists.
            var sender = Object.FindAnyObjectByType<QuestMidiSender>();
            if (sender != null) visor.sender = sender;

            Undo.RegisterCreatedObjectUndo(go, "Add GANTASMO Visor");
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);

            if (sender == null)
            {
                EditorUtility.DisplayDialog(
                    "GANTASMO Visor added",
                    "Added the visor to the scene.\n\nNo QuestMidiSender was found — add one " +
                    "(Quest MIDI Bridge ▸ Setup Wizard) so the visor can react to the DAW's " +
                    "return MIDI on the \"QuestMIDI-Return\" port.",
                    "OK");
            }
        }
    }
}
