#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gantasmo.XrViz.EditorTools
{
    /// <summary>
    /// One-click scene setup for the XR visualization feed. Adds a receiver (if
    /// missing) and a waveform ribbon placed in front of the performer at eye
    /// height, matching the "waveforms up top" layout rule. Requires a
    /// QuestMidiSender already in the scene (the bridge that carries the feed).
    /// </summary>
    public static class XrVizSetup
    {
        [MenuItem("GANTASMO/Visuals/Add Waveform Ribbon", priority = 100)]
        public static void AddWaveformRibbon()
        {
            var receiver = Object.FindAnyObjectByType<XrVizReceiver>();
            if (receiver == null)
            {
                var rgo = new GameObject("XR Viz Receiver");
                receiver = rgo.AddComponent<XrVizReceiver>();
                Undo.RegisterCreatedObjectUndo(rgo, "Add XR Viz Receiver");
            }

            var go = new GameObject("Waveform Ribbon");
            Undo.RegisterCreatedObjectUndo(go, "Add Waveform Ribbon");

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.widthMultiplier = 0.01f;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.alignment = LineAlignment.View;

            // Pick a shader that renders on the Quest's pipeline; fall back across
            // the common options so the line is visible without manual setup.
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");
            if (shader != null) lr.material = new Material(shader);
            lr.startColor = new Color(0.2f, 0.85f, 1f);
            lr.endColor = new Color(0.85f, 0.2f, 1f);

            var ribbon = go.AddComponent<XrWaveformRibbon>();
            ribbon.receiver = receiver;

            // Eye height, ~1.5 m ahead, slightly above center so it reads as a
            // header. Adjust in-scene to taste.
            go.transform.position = new Vector3(0f, 1.6f, 1.5f);

            Selection.activeGameObject = go;
            if (Object.FindAnyObjectByType<QuestMidiBridge.QuestMidiSender>() == null)
            {
                Debug.LogWarning("[XrViz] Added the waveform ribbon, but no QuestMidiSender " +
                                 "is in the scene yet. Add the MIDI bridge so the feed can arrive.");
            }
            else
            {
                Debug.Log("[XrViz] Added waveform ribbon + receiver. Play theDAW audio to see it move.");
            }
        }
    }
}
#endif
